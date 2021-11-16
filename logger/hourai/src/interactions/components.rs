use crate::{
    http,
    interactions::{InteractionContext, InteractionError, InteractionResult},
    models::{
        application::interaction::message_component::MessageComponentInteraction,
        guild::PartialMember,
        id::{ApplicationId, ChannelId, GuildId, InteractionId},
        user::User,
    },
    proto::message_components::MessageComponentProto,
};
use anyhow::Result;
use protobuf::Message;
use std::sync::Arc;

pub fn proto_to_custom_id(proto: &impl Message) -> Result<String> {
    Ok(base64::encode(proto.write_to_bytes()?))
}

#[derive(Clone)]
pub struct ComponentContext {
    pub http: Arc<http::Client>,
    pub component: Box<MessageComponentInteraction>,
}

impl ComponentContext {
    pub fn metadata(&self) -> Result<MessageComponentProto> {
        let decoded = base64::decode(&self.component.data.custom_id)?;
        Ok(MessageComponentProto::parse_from_bytes(&decoded)?)
    }
}

impl InteractionContext for ComponentContext {
    fn http(&self) -> &Arc<http::Client> {
        &self.http
    }

    fn id(&self) -> InteractionId {
        self.component.id
    }

    fn application_id(&self) -> ApplicationId {
        self.component.application_id
    }

    fn token(&self) -> &str {
        &self.component.token
    }

    fn guild_id(&self) -> InteractionResult<GuildId> {
        self.component.guild_id.ok_or(InteractionError::NotInGuild)
    }

    fn channel_id(&self) -> ChannelId {
        self.component.channel_id
    }

    fn member(&self) -> Option<&PartialMember> {
        self.component.member.as_ref()
    }

    fn user(&self) -> &User {
        let member = self
            .component
            .member
            .as_ref()
            .and_then(|member| member.user.as_ref());
        let user = self.component.user.as_ref();
        user.or(member).unwrap()
    }
}
