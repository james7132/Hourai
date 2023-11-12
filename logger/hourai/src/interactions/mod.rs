mod commands;
mod components;

pub use commands::*;
pub use components::*;

use crate::{
    http,
    models::{
        channel::message::{embed::Embed, MessageFlags, component::Component},
        guild::{PartialMember, Permissions},
        http::interaction::*,
        id::{
            marker::{ApplicationMarker, ChannelMarker, GuildMarker, InteractionMarker},
            Id,
        },
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
    #[error("This command has not been implemented yet. Check back later.")]
    NotImplemented,
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

pub struct Response(InteractionResponseData);

impl Response {
    pub fn direct() -> Self {
        Self(InteractionResponseData {
            allowed_mentions: None,
            attachments: None,
            choices: None,
            components: None,
            content: None,
            custom_id: None,
            embeds: None,
            flags: None,
            title: None,
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
        if let Some(embeds) = self.0.embeds.as_mut() {
            embeds.push(embed.into())
        } else {
            self.0.embeds = Some(vec![embed.into()]);
        }
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

impl From<Response> for InteractionResponseData {
    fn from(value: Response) -> Self {
        value.0
    }
}

#[async_trait::async_trait]
pub trait InteractionContext {
    fn http(&self) -> &Arc<http::Client>;
    fn id(&self) -> Id<InteractionMarker>;
    fn application_id(&self) -> Id<ApplicationMarker>;
    fn token(&self) -> &str;
    fn member(&self) -> Option<&PartialMember>;
    fn guild_id(&self) -> InteractionResult<Id<GuildMarker>>;
    fn channel_id(&self) -> Id<ChannelMarker>;
    fn user(&self) -> &User;

    async fn defer(&self) -> anyhow::Result<()> {
        let response = Response::direct();
        let response = InteractionResponse {
            kind: InteractionResponseType::DeferredChannelMessageWithSource,
            data: Some(response.into()),
        };
        self.reply_raw(response).await?;
        Ok(())
    }

    async fn defer_ephemeral(&self) -> anyhow::Result<()> {
        let response = Response::ephemeral();
        let response = InteractionResponse {
            kind: InteractionResponseType::DeferredChannelMessageWithSource,
            data: Some(response.into()),
        };
        self.reply_raw(response).await?;
        Ok(())
    }

    async fn defer_update(&self) -> anyhow::Result<()> {
        self.reply_raw(InteractionResponse {
            kind: InteractionResponseType::DeferredUpdateMessage,
            data: None,
        })
        .await?;
        Ok(())
    }

    async fn reply_raw(&self, response: InteractionResponse) -> anyhow::Result<()> {
        self.http()
            .interaction(self.application_id())
            .create_response(self.id(), self.token(), &response)
            .await?;
        Ok(())
    }

    async fn reply(
        &self,
        data: impl Into<InteractionResponseData> + Send + 'static,
    ) -> anyhow::Result<()> {
        let data = data.into();
        self.http()
            .interaction(self.application_id())
            .update_response(self.token())
            .content(data.content.as_deref())?
            .embeds(data.embeds.as_deref())?
            .components(data.components.as_deref())?
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
