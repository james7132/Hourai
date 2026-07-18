pub mod approvers;
pub mod context;
pub mod rejectors;
pub mod verifier;

pub use context::*;
pub use verifier::*;

use anyhow::Result;
use hourai::cache::InMemoryCache;
use hourai::models::guild::Member;
use hourai::models::id::{Id, marker::GuildMarker};
use hourai::proto::guild_configs::{LoggingConfig, VerificationConfig};
use hourai_sql::SqlPool;
use twilight_util::builder::embed::*;

pub fn make_verifiers(cache: InMemoryCache, sql: SqlPool) -> Vec<BoxedVerifier> {
    vec![
        rejectors::new_account(chrono::Duration::days(30)),
        rejectors::no_avatar(),
        rejectors::deleted_user(sql.clone()),
        approvers::nitro(),
        rejectors::banned_user(sql.clone(), /* min_guild_size */ 150),
        rejectors::banned_username(sql.clone()),
        rejectors::username_match(
            sql.clone(),
            "Offensive username. ",
            vec!["nigger", "nigga", "faggot", "cuck", "retard"],
        ),
        rejectors::username_match(
            sql,
            "Sexually inappropriate username. ",
            vec![
                "anal", "cock", "vore", "scat", "fuck", "pussy", "urethra", "rape", "penis",
                "piss", "shit", "cum",
            ],
        ),
        approvers::distinguished_user(cache),
        approvers::bot(),
        approvers::bot_owners(vec![]),
    ]
}

pub async fn verify_member(
    guild_id: Id<GuildMarker>,
    member: &Member,
    verifiers: &[BoxedVerifier],
) -> Result<VerificationContext> {
    let mut ctx = VerificationContext::new(guild_id, member.clone());
    for v in verifiers {
        v.verify(&mut ctx).await?;
    }
    Ok(ctx)
}

pub async fn on_member_join(
    client: &crate::Client,
    guild_id: Id<GuildMarker>,
    member: &Member,
) -> Result<()> {
    let config: VerificationConfig = client
        .storage()
        .redis()
        .guild(guild_id)
        .configs()
        .get()
        .await?;
    if !config.get_enabled() {
        return Ok(());
    }

    let verifiers = make_verifiers(client.0.cache.clone(), client.storage().sql().clone());
    let ctx = verify_member(guild_id, member, &verifiers).await?;

    if ctx.is_approved() {
        if config.has_role_id() {
            let role_id = Id::new(config.get_role_id());
            let _ = client
                .http()
                .add_guild_member_role(guild_id, member.user.id, role_id)
                .await;
        }
    } else {
        let logging_config: LoggingConfig = client
            .storage()
            .redis()
            .guild(guild_id)
            .configs()
            .get()
            .await?;
        if logging_config.has_modlog_channel_id() {
            let channel_id = Id::new(logging_config.get_modlog_channel_id());
            let user = &member.user;
            let mut desc = format!("**User:** <@{}> ({})\n", user.id, user.name);
            let rejection_list: Vec<&str> = ctx.rejection_reasons().collect();
            if !rejection_list.is_empty() {
                desc.push_str("\n**Rejection Reasons:**\n");
                for r in rejection_list {
                    desc.push_str(&format!("• {}\n", r));
                }
            }
            let embed = EmbedBuilder::new()
                .title("⚠️ User Verification Required")
                .description(desc)
                .color(0xED4245)
                .footer(EmbedFooterBuilder::new(format!("{:x}", user.id.get())))
                .build();

            let _ = client
                .http()
                .create_message(channel_id)
                .embeds(&[embed])
                .await;
        }
    }

    Ok(())
}
