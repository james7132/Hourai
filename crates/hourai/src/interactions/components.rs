use crate::{
    http,
    interactions::{InteractionContext, InteractionError, InteractionResult},
    models::{
        application::interaction::{
            Interaction, InteractionData, message_component::MessageComponentInteractionData,
        },
        guild::PartialMember,
        id::{
            Id,
            marker::{ApplicationMarker, ChannelMarker, GuildMarker, InteractionMarker},
        },
        user::User,
    },
    proto::message_components::MessageComponentProto,
};
use anyhow::Result;
use protobuf::Message;
use std::marker::PhantomData;
use std::sync::Arc;

pub const DISCORD_CUSTOM_ID_MAX_LEN: usize = 100;

pub fn proto_to_custom_id(proto: &impl Message) -> Result<String> {
    let encoded = base64::encode(proto.write_to_bytes()?);
    if encoded.len() > DISCORD_CUSTOM_ID_MAX_LEN {
        anyhow::bail!(
            "custom_id length {} exceeds Discord maximum limit of {}",
            encoded.len(),
            DISCORD_CUSTOM_ID_MAX_LEN
        );
    }
    Ok(encoded)
}

pub fn parse_custom_id<T: Message + Default>(custom_id: &str) -> Result<T> {
    let decoded = base64::decode(custom_id)?;
    Ok(T::parse_from_bytes(&decoded)?)
}

#[derive(Clone)]
pub struct ComponentContext {
    pub http: Arc<http::Client>,
    pub component: Interaction,
    marker_: PhantomData<()>,
}

impl ComponentContext {
    pub fn new(client: Arc<http::Client>, interaction: Interaction) -> Self {
        assert!(matches!(
            interaction.data,
            Some(InteractionData::MessageComponent(_))
        ));
        Self {
            http: client,
            component: interaction,
            marker_: PhantomData,
        }
    }

    #[expect(clippy::expect_used)]
    fn data(&self) -> &MessageComponentInteractionData {
        match &self.component.data {
            Some(InteractionData::MessageComponent(data)) => data,
            _ => Option::<&MessageComponentInteractionData>::None
                .expect("Provided interaction data is not a message component"),
        }
    }

    pub fn metadata(&self) -> Result<MessageComponentProto> {
        parse_custom_id(&self.data().custom_id)
    }
}

impl InteractionContext for ComponentContext {
    fn http(&self) -> &Arc<http::Client> {
        &self.http
    }

    fn id(&self) -> Id<InteractionMarker> {
        self.component.id
    }

    fn application_id(&self) -> Id<ApplicationMarker> {
        self.component.application_id
    }

    fn token(&self) -> &str {
        &self.component.token
    }

    fn guild_id(&self) -> InteractionResult<Id<GuildMarker>> {
        self.component.guild_id.ok_or(InteractionError::NotInGuild)
    }

    #[expect(clippy::expect_used)]
    fn channel_id(&self) -> Id<ChannelMarker> {
        self.component
            .channel
            .as_ref()
            .map(|c| c.id)
            .expect("Component interaction is missing channel")
    }

    fn member(&self) -> Option<&PartialMember> {
        self.component.member.as_ref()
    }

    #[expect(clippy::expect_used)]
    fn user(&self) -> &User {
        let member = self
            .component
            .member
            .as_ref()
            .and_then(|member| member.user.as_ref());
        let user = self.component.user.as_ref();
        user.or(member)
            .expect("Interaction has neither user nor member")
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::proto::message_components::{VerificationButton, VerificationButtonOption};

    #[test]
    fn test_proto_to_custom_id_within_discord_limit() {
        let mut vbtn = VerificationButton::new();
        vbtn.set_button_option(VerificationButtonOption::VERIFICATION_BUTTON_VERIFY);
        vbtn.set_user_id(123456789012345678);

        let mut proto = MessageComponentProto::new();
        proto.set_verification_button(vbtn);

        let custom_id = proto_to_custom_id(&proto).expect("Encoding should succeed");
        assert!(
            custom_id.len() <= DISCORD_CUSTOM_ID_MAX_LEN,
            "custom_id length {} exceeded limit {}",
            custom_id.len(),
            DISCORD_CUSTOM_ID_MAX_LEN
        );

        let decoded_proto: MessageComponentProto =
            parse_custom_id(&custom_id).expect("Protobuf parse should succeed");

        assert_eq!(
            decoded_proto.get_verification_button().get_user_id(),
            123456789012345678
        );
        assert_eq!(
            decoded_proto.get_verification_button().get_button_option(),
            VerificationButtonOption::VERIFICATION_BUTTON_VERIFY
        );
    }
}
