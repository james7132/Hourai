use anyhow::Result;
use crate::{
    http::Client,
    models::{
        application::{
            callback::{CallbackData, InteractionResponse},
            interaction::application_command::{
                ApplicationCommand, CommandDataOption, CommandOptionValue, InteractionMember,
            },
        },
        channel::{embed::Embed, message::MessageFlags},
        guild::{PartialMember, Permissions},
        id::{GuildId, UserId},
        user::User,
    },
};
use std::str::FromStr;
use std::sync::Arc;
use thiserror::Error;

pub type CommandResult<T> = std::result::Result<T, CommandError>;

#[derive(Debug, Error)]
pub enum CommandError {
    #[error("Unkown command. This command is currently unsuable.")]
    UnknownCommand,
    #[error("Command can only be used in a server.")]
    NotInGuild,
    #[error("User is missing permission: `{0}`")]
    MissingPermission(&'static str),
    #[error("{0}")]
    UserError(&'static str),
    #[error("Generic error: {0}")]
    GenericError(#[from] anyhow::Error),
}

#[derive(Debug)]
pub enum Command<'a> {
    Command(&'a str),
    SubCommand(&'a str, &'a str),
    SubGroupCommand(&'a str, &'a str, &'a str),
}

pub struct Response {
    data: CallbackData,
}

impl Response {
    pub fn direct() -> Self {
        Self {
            data: CallbackData {
                allowed_mentions: None,
                components: None,
                content: None,
                embeds: Vec::new(),
                flags: None,
                tts: None,
            },
        }
    }

    pub fn ephemeral() -> Self {
        Self {
            data: CallbackData {
                allowed_mentions: None,
                components: None,
                content: None,
                embeds: Vec::new(),
                flags: Some(MessageFlags::EPHEMERAL),
                tts: None,
            },
        }
    }

    pub fn content(mut self, content: impl Into<String>) -> Self {
        self.data.content = Some(content.into());
        self
    }

    pub fn embed(mut self, embed: impl Into<Embed>) -> Self {
        self.data.embeds.push(embed.into());
        self
    }
}

impl From<Response> for CallbackData {
    fn from(value: Response) -> Self {
        value.data
    }
}

pub struct CommandContext {
    pub http: Arc<Client>,
    pub command: Box<ApplicationCommand>,
}

impl CommandContext {
    pub fn guild_id(&self) -> CommandResult<GuildId> {
        self.command.guild_id.ok_or(CommandError::NotInGuild)
    }

    pub fn user(&self) -> Option<&User> {
        self.command.user.as_ref()
    }

    pub fn member(&self) -> Option<&PartialMember> {
        self.command.member.as_ref()
    }

    pub fn command<'a>(&'a self) -> Command<'a> {
        let base = self.command.data.name.as_ref();
        if let Some((sub, options)) = Self::get_subcommand(&self.command.data.options) {
            if let Some((subsub, _)) = Self::get_subcommand(options) {
                Command::SubGroupCommand(base, sub, subsub)
            } else {
                Command::SubCommand(base, sub)
            }
        } else {
            Command::Command(base)
        }
    }

    /// Checks if the caller has a given set of permissions. All provided permissions must be
    /// present for this to return true.
    pub fn has_user_permission(&self, perms: Permissions) -> bool {
        self.command
            .member
            .as_ref()
            .and_then(|m| m.permissions)
            .map(|p| p.contains(perms))
            .unwrap_or(false)
    }

    /// Gets the first instance of an option containing a specific name substring, if available.
    pub fn options(&self) -> impl Iterator<Item = &CommandDataOption> {
        Self::flatten_options(&self.command.data.options).into_iter()
    }

    pub fn option_named(&self, name: &'static str) -> Option<&CommandDataOption> {
        self.options().find(move |opt| opt.name.contains(name))
    }

    pub fn all_options_named(
        &self,
        name: &'static str,
    ) -> impl Iterator<Item = &CommandDataOption> {
        self.options().filter(move |opt| opt.name.contains(name))
    }

    /// Gets all of the raw IDs with a given name.
    pub fn all_id_options_named(&self, name: &'static str) -> impl Iterator<Item = u64> + '_ {
        self.all_options_named(name)
            .filter_map(Self::parse_option_id)
    }

    /// Checks if a boolean flag is set to true or not. If no flag with the name was found, it
    /// returs None.
    pub fn get_string(&self, name: &'static str) -> Option<&String> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::String(ref value) => Some(value),
                _ => None,
            })
    }

    /// Checks if a boolean flag is set to true or not. If no flag with the name was found, it
    /// returs None.
    pub fn get_flag(&self, name: &'static str) -> Option<bool> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::Boolean(value) => Some(value),
                _ => None,
            })
    }

    pub fn resolve_user(&self, id: UserId) -> Option<&User> {
        self.command
            .data
            .resolved
            .as_ref()
            .and_then(|r| r.users.iter().find(|m| m.id == id))
    }

    pub fn resolve_member(&self, id: UserId) -> Option<&InteractionMember> {
        self.command
            .data
            .resolved
            .as_ref()
            .and_then(|r| r.members.iter().find(|m| m.id == id))
    }

    fn parse_option_id(option: &CommandDataOption) -> Option<u64> {
        if let CommandOptionValue::String(ref value) = option.value {
            u64::from_str(value).ok()
        } else {
            None
        }
    }

    fn flatten_options(options: &Vec<CommandDataOption>) -> Vec<&CommandDataOption> {
        let mut all_options: Vec<&CommandDataOption> = Vec::new();
        for option in options.iter() {
            if let CommandOptionValue::SubCommand(ref options) = option.value {
                all_options.extend(Self::flatten_options(options));
            } else {
                all_options.push(option);
            }
        }
        all_options
    }

    pub async fn defer(&self, data: impl Into<CallbackData>) -> Result<()> {
        let response = InteractionResponse::DeferredChannelMessageWithSource(data.into());
        self.reply_raw(response).await?;
        Ok(())
    }

    pub async fn reply(&self, data: impl Into<CallbackData>) -> Result<()> {
        let response = InteractionResponse::ChannelMessageWithSource(data.into());
        self.reply_raw(response).await?;
        Ok(())
    }

    pub async fn reply_raw(&self, response: InteractionResponse) -> Result<()> {
        self.http
            .interaction_callback(self.command.id, &self.command.token, &response)
            .exec()
            .await?;
        Ok(())
    }

    pub async fn update(&self, content: String) -> Result<()> {
        self.http
            .update_interaction_original(&self.command.token)?
            .content(Some(&content))?
            .exec()
            .await?;
        Ok(())
    }

    fn get_subcommand<'a>(
        options: &'a Vec<CommandDataOption>,
    ) -> Option<(&'a str, &'a Vec<CommandDataOption>)> {
        for option in options.iter() {
            if let CommandOptionValue::SubCommand(ref options) = option.value {
                return Some((option.name.as_ref(), options));
            }
        }
        None
    }
}
