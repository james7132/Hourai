use hourai::{
    interactions::proto_to_custom_id,
    models::{
        application::component::{button::ButtonStyle, *},
        channel::ReactionType,
    },
    proto::message_components::*,
};

const PREVIOUS_BUTTON: &str = "◀️";
const NEXT_BUTTON: &str = "▶️";
const PLAY_BUTTON: &str = "⏯️";
const STOP_BUTTON: &str = "⏹️";
const NEXT_TRACK_BUTTON: &str = "⏭️";
const VOLUME_DOWN_BUTTON: &str = "🔉";
const VOLUME_UP_BUTTON: &str = "🔊";

#[inline(always)]
pub fn previous_button(ui_type: MusicUIType) -> Component {
    create_button(
        PREVIOUS_BUTTON,
        ui_type,
        MusicButtonOption::MUSIC_BUTTON_QUEUE_PREV_PAGE,
    )
}

#[inline(always)]
pub fn next_button(ui_type: MusicUIType) -> Component {
    create_button(
        NEXT_BUTTON,
        ui_type,
        MusicButtonOption::MUSIC_BUTTON_QUEUE_NEXT_PAGE,
    )
}

#[inline(always)]
pub fn play_button(ui_type: MusicUIType) -> Component {
    create_button(
        PLAY_BUTTON,
        ui_type,
        MusicButtonOption::MUSIC_BUTTON_PLAY_PAUSE,
    )
}

#[inline(always)]
pub fn stop_button(ui_type: MusicUIType) -> Component {
    create_button(STOP_BUTTON, ui_type, MusicButtonOption::MUSIC_BUTTON_STOP)
}

#[inline(always)]
pub fn skip_button(ui_type: MusicUIType) -> Component {
    create_button(
        NEXT_TRACK_BUTTON,
        ui_type,
        MusicButtonOption::MUSIC_BUTTON_NEXT_TRACK,
    )
}

#[inline(always)]
pub fn volume_down_button(ui_type: MusicUIType) -> Component {
    create_button(
        VOLUME_DOWN_BUTTON,
        ui_type,
        MusicButtonOption::MUSIC_BUTTON_VOLUME_DOWN,
    )
}

#[inline(always)]
pub fn volume_up_button(ui_type: MusicUIType) -> Component {
    create_button(
        VOLUME_UP_BUTTON,
        ui_type,
        MusicButtonOption::MUSIC_BUTTON_VOLUME_UP,
    )
}

fn create_button(emoji: &str, ui_type: MusicUIType, button: MusicButtonOption) -> Component {
    let mut proto = MessageComponentProto::new();
    proto.mut_music_button().set_field_type(ui_type);
    proto.mut_music_button().set_button_option(button);
    Component::Button(Button {
        custom_id: Some(proto_to_custom_id(&proto).unwrap()),
        disabled: false,
        emoji: Some(ReactionType::Unicode {
            name: emoji.to_owned(),
        }),
        label: None,
        style: ButtonStyle::Secondary,
        url: None,
    })
}
