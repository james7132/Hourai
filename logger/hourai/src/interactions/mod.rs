mod commands;
mod components;

pub use commands::*;
pub use components::*;

use crate::{
    http,
    models::{
        application::{
            callback::{CallbackData, InteractionResponse},
            component::Component,
        },
        channel::{embed::Embed, message::MessageFlags},
        guild::{PartialMember, Permissions},
        id::{ApplicationId, ChannelId, GuildId, InteractionId},
        user::User,
    },
};
use std::sync::Arc;
use thiserror::Error;

pub type InteractionResult<T> = std::result::Result<T, InteractionError>;

#[derive(Debug, Error)]
pub enum InteractionError {
    #[error("Unkown command. This command is currently unsuable.")]
    UnknownCommand,
    #[error("Command can only be used in a server.")]
    NotInGuild,
    #[error("Missing argument: {}", .0)]
    MissingArgument(&'static str),
    #[error("Invalid argument: {}", .0)]
    InvalidArgument(String),
    #[error("User failed to satisfy preconditions: {}", .0)]
    FailedPrecondition(&'static str),
    #[error("User is missing permission: `{0}`")]
    MissingPermission(&'static str),
    #[error("{0}")]
    UserError(&'static str),
}

pub struct Response(CallbackData);

impl Response {
    pub fn direct() -> Self {
        Self(CallbackData {
            allowed_mentions: None,
            components: None,
            content: None,
            embeds: Vec::new(),
            flags: None,
            tts: None,
        })
    }

    pub fn ephemeral() -> Self {
        Self::direct().flag(MessageFlags::EPHEMERAL)
    }

    pub fn content(mut self, content: impl Into<String>) -> Self {
        self.0.content = Some(content.into());
        self
    }

    pub fn embed(mut self, embed: impl Into<Embed>) -> Self {
        self.0.embeds.push(embed.into());
        self
    }

    pub fn flag(mut self, flags: impl Into<MessageFlags>) -> Self {
        self.0.flags = Some(flags.into() | self.0.flags.unwrap_or(MessageFlags::empty()));
        self
    }

    pub fn components(mut self, components: &[Component]) -> Self {
        if let Some(ref mut comps) = self.0.components {
            comps.extend(components.iter().cloned());
        } else {
            self.0.components = Some(Vec::from(components));
        }
        self
    }
}

impl From<Response> for CallbackData {
    fn from(value: Response) -> Self {
        value.0
    }
}

#[async_trait::async_trait]
pub trait InteractionContext {
    fn http(&self) -> &Arc<http::Client>;
    fn id(&self) -> InteractionId;
    fn application_id(&self) -> ApplicationId;
    fn token(&self) -> &str;
    fn member(&self) -> Option<&PartialMember>;
    fn guild_id(&self) -> InteractionResult<GuildId>;
    fn channel_id(&self) -> ChannelId;
    fn user(&self) -> &User;

    async fn defer(
        &self,
        data: impl Into<CallbackData> + Send + 'async_trait,
    ) -> anyhow::Result<()> {
        let response = InteractionResponse::DeferredChannelMessageWithSource(data.into());
        self.reply_raw(response).await?;
        Ok(())
    }

    async fn defer_update(&self) -> anyhow::Result<()> {
        self.reply_raw(InteractionResponse::DeferredUpdateMessage)
            .await?;
        Ok(())
    }

    async fn reply(
        &self,
        data: impl Into<CallbackData> + Send + 'async_trait,
    ) -> anyhow::Result<()> {
        let response = InteractionResponse::ChannelMessageWithSource(data.into());
        self.reply_raw(response).await?;
        Ok(())
    }

    async fn reply_raw(&self, response: InteractionResponse) -> anyhow::Result<()> {
        self.http()
            .interaction_callback(self.id(), self.token(), &response)
            .exec()
            .await?;
        Ok(())
    }

    async fn update(&self, content: String) -> anyhow::Result<()> {
        self.http()
            .update_interaction_original(self.token())?
            .content(Some(&content))?
            .exec()
            .await?;
        Ok(())
    }

    /// Checks if the caller has a given set of permissions. All provided permissions must be
    /// present for this to return true.
    fn has_user_permission(&self, perms: Permissions) -> bool {
        self.member()
            .and_then(|m| m.permissions)
            .map(|p| p.contains(perms))
            .unwrap_or(false)
    }
}
