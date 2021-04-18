#[macro_use]
extern crate lazy_static;

mod approvers;
mod context;
mod rejectors;
mod verifier;

use anyhow::Result;
use chrono::Duration;
use hourai::{
    init, config,
    models::{id::*, guild::Member, channel::Message},
    cache::{ResourceType, InMemoryCache},
    gateway::{Intents, Event, EventTypeFlags, Cluster, cluster::ShardScheme}
};
use tracing::{info, error};
use std::collections::HashSet;
use std::sync::Arc;
use futures::prelude::*;

const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits()
        | Intents::GUILD_MESSAGES.bits()
        | Intents::GUILD_MEMBERS.bits()
);

const BOT_EVENTS: EventTypeFlags = EventTypeFlags::from_bits_truncate(
        EventTypeFlags::MEMBER_ADD.bits()
        | EventTypeFlags::MEMBER_UPDATE.bits()
);

fn full_match(list: Vec<String>) -> Vec<String> {
    list.into_iter()
        .map(|s| format!(r"\A{}\z", s))
        .collect()
}

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());

    init::init(&config);
    let http_client = init::http_client(&config);
    let sql = hourai_sql::init(&config).await;
    let redis = hourai_redis::init(&config).await;
    let cache = InMemoryCache::builder()
        .resource_types(ResourceType::GUILD)
        .build();
    let gateway = init::cluster(&config, BOT_INTENTS)
        .shard_scheme(ShardScheme::Auto)
        .http_client(http_client.clone())
        .build()
        .await
        .expect("Failed to connect to the Discord gateway");

    let application = http_client
        .current_user_application()
        .await
        .expect("Bot application should not fail to load.");

    let mut owners: HashSet<UserId> = HashSet::new();
    owners.insert(application.owner.id);
    if let Some(team) = application.team {
        owners.insert(team.owner_user_id);
        for member in team.members {
            owners.insert(member.user.id);
        }
    }

    let mut verifiers: Vec<verifier::BoxedVerifier> = Vec::new();
    // Suspicion Level Verifiers
    //     Verifiers here are mostly for suspicious characteristics.
    //     These are designed with a high-recall, low precision methdology.
    //     False positives from these are more likely.  These are low severity
    //     checks.

    // New user accounts are commonly used for alts of banned users.
    verifiers.push(rejectors::new_account(Duration::days(30)));  // 30 days
    // Low effort user bots and alt accounts tend not to set an avatar.
    verifiers.push(rejectors::no_avatar());
    // Deleted accounts shouldn't be able to join new servers. A user
    // joining that is seemingly deleted is suspicious.
    verifiers.push(rejectors::deleted_user(sql.clone()));

    // Filter likely user bots based on usernames.
    verifiers.push(Box::new(rejectors::UsernameMatchRejector::new(
        /*sql=*/ sql.clone(),
        /*prefix=*/ "Likely user bot. ",
        /*matches=*/ config.load_list("user_bot_names"))
            .expect("Could not create username match rejector.")));
    verifiers.push(Box::new(rejectors::UsernameMatchRejector::new(
        /*sql=*/sql.clone(),
        /*prefix=*/"Likely user bot. ",
        /*matches=*/full_match(config.load_list("user_bot_names_fullmatch")))
            .expect("Could not create username match rejector.")));

    // If a user has Nitro, they probably aren't an alt or user bot.
    verifiers.push(approvers::nitro());

    // Questionable Level Verifiers
    //     Verifiers here are mostly for red flags of unruly or
    //     potentially troublesome.  These are designed with a
    //     high-recall, high-precision methdology. False positives from
    //     these are more likely to occur.

    // Filter usernames and nicknames that match moderator users.
    //verifiers.push(
    //rejectors.NameMatchRejector(
        //prefix="Username matches moderator\'s. ",
        //filter_func=utils.is_moderator,
        //min_match_length=4),
    //verifiers.push(
    //rejectors.NameMatchRejector(
        //prefix="Username matches moderator\'s. ",
        //filter_func=utils.is_moderator,
        //member_selector=lambda m: m.nick,
        //min_match_length=4),

    // Filter usernames and nicknames that match bot users.
    //verifiers.push(
    //rejectors.NameMatchRejector(
        //prefix="Username matches bot\'s. ",
        //filter_func=lambda m: m.bot,
        //min_match_length=4),
    //verifiers.push(
    //rejectors.NameMatchRejector(
        //prefix="Username matches bot\'s. ",
        //filter_func=lambda m: m.bot,
        //member_selector=lambda m: m.nick,
        //min_match_length=4),

    // Filter offensive usernames.
    verifiers.push(Box::new(rejectors::UsernameMatchRejector::new(
        /*sql=*/sql.clone(),
        /*prefix=*/"Offensive username. ",
        /*matches=*/config.load_list("offensive_usernames"))
            .expect("Could not create username match rejector.")));
    // Filter sexually inapproriate usernames.
    verifiers.push(Box::new(rejectors::UsernameMatchRejector::new(
        /*sql=*/sql.clone(),
        /*prefix=*/"Sexually inappropriate username. ",
        /*matches=*/config.load_list("sexually_inapproriate_usernames"))
            .expect("Could not create username match rejector.")));

    // Filter potentially long usernames that use wide unicode characters that
    // may be disruptive or spammy to other members.
    // TODO(james7132): Reenable wide unicode character filter

    // Malicious Level Verifiers
    //     Verifiers here are mostly for known offenders.
    //     These are designed with a low-recall, high precision
    //     methdology. False positives from these are far less likely to
    //     occur.

    // Make sure the user is not banned on other servers.
    verifiers.push(rejectors::banned_user(sql.clone(), 150));

    // Check the username against known banned users from the current
    // server. Requires exact username match (case insensitive)
    verifiers.push(rejectors::banned_username(sql.clone()));

    // Check if the user is distinguished (Discord Staff, Verified, Partnered,
    // etc).
    verifiers.push(approvers::distinguished_user(cache.clone()));

    // All non-override users are rejected while guilds are locked down.
    //verifiers.push( rejectors.LockdownRejector(),

    // Override Level Verifiers
    //     Verifiers here are made to explictly override previous
    //     verifiers. These are specifically targetted at a small
    //     specific group of individiuals. False positives and negatives
    //     at this level are very unlikely if not impossible.
    verifiers.push(approvers::bot());
    verifiers.push(approvers::bot_owners(owners));

    let client = {
        let user = http_client
            .current_user()
            .await
            .expect("User should not fail to load.");
        Client(Arc::new(ClientRef {
            user_id: user.id,
            cache: cache.clone(),
            verifiers: verifiers
        }))
    };

    info!("Starting gateway...");
    gateway.up().await;
    info!("Client started.");

    let mut events = gateway.some_events(BOT_EVENTS);
    while let Some((shard_id, evt)) = events.next().await {
        client.pre_cache_event(&evt).await;
        cache.update(&evt);
        tokio::spawn(client.clone().consume_event(shard_id, evt));
    }

    info!("Shutting down gateway...");
    gateway.down();
    info!("Client stopped.");
}

pub struct ClientRef {
    user_id: UserId,
    cache: InMemoryCache,
    verifiers: Vec<verifier::BoxedVerifier>,
}

#[derive(Clone)]
pub struct Client(Arc<ClientRef>);

impl Client {
    /// Handle events before the cache is updated.
    async fn pre_cache_event(&self, event: &Event) -> () {
        let kind = event.kind();
        let result = match event {
            Event::MemberUpdate(ref evt) => {
                if self.0.cache.is_pending(evt.guild_id, evt.user.id) && !evt.pending {
                    let member = Member {
                        guild_id: evt.guild_id,
                        nick: evt.nick.clone(),
                        pending: false,
                        premium_since: evt.premium_since.clone(),
                        roles: evt.roles.clone(),
                        user: evt.user.clone(),
                        joined_at: Some(evt.joined_at.clone()),

                        // Unknown/dummy fields.
                        hoisted_role: None,
                        deaf: false,
                        mute: false,
                    };
                    self.on_member_add(member).await
                } else {
                    Ok(())
                }
            },
            _ => Ok(())
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {}", kind, err);
        }
    }

    async fn consume_event(self, shard_id: u64, event: Event) -> () {
        let kind = event.kind();
        let result = match event {
            Event::MemberAdd(evt) => self.on_member_add(evt.0).await,
            Event::MemberUpdate(evt) => Ok(()),
            Event::MessageCreate(evt) => self.on_message_create(evt.0).await,
            _ => {
                error!("Unexpected event type: {:?}", event);
                Ok(())
            }
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {}", kind, err);
        }
    }

    async fn on_message_create(self, evt: Message) -> Result<()> {
        Ok(())
    }

    async fn on_member_add(&self, member: Member) -> Result<()> {
        Ok(())
    }

}
