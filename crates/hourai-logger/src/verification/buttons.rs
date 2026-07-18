use anyhow::Result;
use hourai::{
    http::request::AuditLogReason,
    interactions::{InteractionContext, Response},
    models::{
        channel::message::{
            Component,
            component::{ActionRow, Button, ButtonStyle},
        },
        guild::Permissions,
        id::{Id, marker::*},
    },
    proto::{
        guild_configs::VerificationConfig,
        message_components::{MessageComponentProto, VerificationButton, VerificationButtonOption},
    },
};
use twilight_model::channel::message::EmojiReactionType;

pub const APPROVE_EMOJI: &str = "✅";
pub const KICK_EMOJI: &str = "❌";
pub const BAN_EMOJI: &str = "☠️";

pub fn verification_buttons(user_id: Id<UserMarker>) -> Result<Component> {
    let verify_btn = create_verification_button(
        user_id,
        VerificationButtonOption::VERIFICATION_BUTTON_VERIFY,
        "Verify",
        APPROVE_EMOJI,
        ButtonStyle::Success,
    )?;

    let kick_btn = create_verification_button(
        user_id,
        VerificationButtonOption::VERIFICATION_BUTTON_KICK,
        "Kick",
        KICK_EMOJI,
        ButtonStyle::Secondary,
    )?;

    let ban_btn = create_verification_button(
        user_id,
        VerificationButtonOption::VERIFICATION_BUTTON_BAN,
        "Ban",
        BAN_EMOJI,
        ButtonStyle::Danger,
    )?;

    Ok(Component::ActionRow(ActionRow {
        components: vec![verify_btn, kick_btn, ban_btn],
    }))
}

fn create_verification_button(
    user_id: Id<UserMarker>,
    option: VerificationButtonOption,
    label: &str,
    emoji: &str,
    style: ButtonStyle,
) -> Result<Component> {
    let mut btn_proto = VerificationButton::new();
    btn_proto.set_button_option(option);
    btn_proto.set_user_id(user_id.get());

    let mut proto = MessageComponentProto::new();
    proto.set_verification_button(btn_proto);

    let custom_id = hourai::interactions::proto_to_custom_id(&proto)?;

    Ok(Component::Button(Button {
        custom_id: Some(custom_id),
        disabled: false,
        emoji: Some(EmojiReactionType::Unicode {
            name: emoji.to_string(),
        }),
        label: Some(label.to_string()),
        sku_id: None,
        style,
        url: None,
    }))
}

pub async fn handle_component_interaction(
    ctx: hourai::interactions::ComponentContext,
    client: &crate::Client,
) -> Result<()> {
    ctx.defer().await?;

    let metadata = ctx.metadata()?;
    if !metadata.has_verification_button() {
        return Ok(());
    }

    let vbtn = metadata.get_verification_button();
    let target_user_id = Id::<UserMarker>::new(vbtn.get_user_id());
    let guild_id = ctx.guild_id()?;

    match vbtn.get_button_option() {
        VerificationButtonOption::VERIFICATION_BUTTON_VERIFY => {
            if !ctx.has_user_permission(Permissions::MANAGE_ROLES)
                && !ctx.has_user_permission(Permissions::MANAGE_GUILD)
            {
                ctx.reply(
                    Response::ephemeral().content("You do not have permission to verify members."),
                )
                .await?;
                return Ok(());
            }

            let config: VerificationConfig = client
                .storage()
                .redis()
                .guild(guild_id)
                .configs()
                .get()
                .await?;

            if config.has_role_id() {
                let role_id = Id::new(config.get_role_id());
                let reason = format!("Manually verified by {}.", ctx.user().name);
                let _ = client
                    .http()
                    .add_guild_member_role(guild_id, target_user_id, role_id)
                    .reason(&reason)
                    .await;
            }

            ctx.reply(Response::direct().content(format!(
                "✅ <@{}> manually verified <@{}>.",
                ctx.user().id,
                target_user_id
            )))
            .await?;
        }
        VerificationButtonOption::VERIFICATION_BUTTON_KICK => {
            if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
                ctx.reply(
                    Response::ephemeral().content("You do not have permission to kick members."),
                )
                .await?;
                return Ok(());
            }

            let reason = format!("Failed verification. Kicked by {}.", ctx.user().name);
            let _ = client
                .http()
                .remove_guild_member(guild_id, target_user_id)
                .reason(&reason)
                .await;

            ctx.reply(Response::direct().content(format!(
                "❌ <@{}> kicked <@{}>.",
                ctx.user().id,
                target_user_id
            )))
            .await?;
        }
        VerificationButtonOption::VERIFICATION_BUTTON_BAN => {
            if !ctx.has_user_permission(Permissions::BAN_MEMBERS) {
                ctx.reply(
                    Response::ephemeral().content("You do not have permission to ban members."),
                )
                .await?;
                return Ok(());
            }

            let reason = format!("Failed verification. Banned by {}.", ctx.user().name);
            let _ = client
                .http()
                .create_ban(guild_id, target_user_id)
                .reason(&reason)
                .await;

            ctx.reply(Response::direct().content(format!(
                "☠️ <@{}> banned <@{}>.",
                ctx.user().id,
                target_user_id
            )))
            .await?;
        }
        _ => {}
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use hourai::interactions::{DISCORD_CUSTOM_ID_MAX_LEN, parse_custom_id};

    #[test]
    fn test_verification_buttons_limit_and_decoding() {
        let user_id = Id::new(987654321098765432);
        let row = verification_buttons(user_id).expect("Should create verification buttons");

        let components = match row {
            Component::ActionRow(ar) => ar.components,
            _ => panic!("Expected ActionRow"),
        };

        assert_eq!(components.len(), 3);

        let expected_options = [
            VerificationButtonOption::VERIFICATION_BUTTON_VERIFY,
            VerificationButtonOption::VERIFICATION_BUTTON_KICK,
            VerificationButtonOption::VERIFICATION_BUTTON_BAN,
        ];

        for (comp, expected_opt) in components.iter().zip(expected_options.iter()) {
            let btn = match comp {
                Component::Button(b) => b,
                _ => panic!("Expected Button"),
            };

            let custom_id = btn.custom_id.as_ref().expect("Button must have custom_id");
            assert!(
                custom_id.len() <= DISCORD_CUSTOM_ID_MAX_LEN,
                "custom_id length {} exceeded {}",
                custom_id.len(),
                DISCORD_CUSTOM_ID_MAX_LEN
            );

            let proto: MessageComponentProto =
                parse_custom_id(custom_id).expect("Protobuf parse failed");

            assert!(proto.has_verification_button());
            let vbtn = proto.get_verification_button();
            assert_eq!(vbtn.get_user_id(), 987654321098765432);
            assert_eq!(vbtn.get_button_option(), *expected_opt);
        }
    }
}
