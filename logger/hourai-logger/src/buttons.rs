use hourai::{
    interactions::proto_to_custom_id,
    models::{
        application::component::{button::ButtonStyle, *},
        channel::ReactionType,
        guild::Permissions,
        id::{marker::UserMarker, Id},
    },
    proto::{action::*, message_components::*},
};

const VERIFY_EMOJI: &str = "✅";
const BAN_EMOJI: &str = "☠️";
const KICK_EMOJI: &str = "❌";
const DELETE_EMOJI: &str = "🗑️";

pub fn ban_button(user_id: Id<UserMarker>, reason: Option<&str>) -> Component {
    let mut action = Action::new();
    action.set_user_id(user_id.get());
    if let Some(reason) = reason {
        action.set_reason(reason.to_owned());
    }
    action.mut_ban().set_field_type(BanMember_Type::BAN);
    create_action_button(BAN_EMOJI, None, Permissions::BAN_MEMBERS, [action])
}

pub fn kick_button(user_id: Id<UserMarker>, reason: Option<&str>) -> Component {
    let mut action = Action::new();
    action.set_user_id(user_id.get());
    if let Some(reason) = reason {
        action.set_reason(reason.to_owned());
    }
    action.set_kick(KickMember::new());
    create_action_button(KICK_EMOJI, None, Permissions::KICK_MEMBERS, [action])
}

pub fn create_action_button(
    emoji: &str,
    label: Option<&str>,
    permissions: Permissions,
    actions: impl IntoIterator<Item = Action>,
) -> Component {
    let mut proto = MessageComponentProto::new();
    proto
        .mut_action_button()
        .set_required_permissions(permissions.bits());
    proto
        .mut_action_button()
        .mut_actions()
        .action
        .extend(actions);
    Component::Button(Button {
        custom_id: Some(proto_to_custom_id(&proto).unwrap()),
        disabled: false,
        emoji: Some(ReactionType::Unicode {
            name: emoji.to_owned(),
        }),
        label: label.map(|s| s.to_owned()),
        style: ButtonStyle::Secondary,
        url: None,
    })
}
