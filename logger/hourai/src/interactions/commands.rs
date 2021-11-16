use crate::{
    http::Client,
    interactions::{InteractionContext, InteractionError, InteractionResult},
    models::{
        application::interaction::application_command::{
            ApplicationCommand, CommandDataOption, CommandOptionValue, InteractionMember,
        },
        guild::{PartialMember, Permissions},
        id::{ApplicationId, ChannelId, GuildId, InteractionId, RoleId, UserId},
        user::User,
    },
};
use std::sync::Arc;

#[derive(Debug)]
pub enum Command<'a> {
    Command(&'a str),
    SubCommand(&'a str, &'a str),
    SubGroupCommand(&'a str, &'a str, &'a str),
}

#[derive(Clone)]
pub struct CommandContext {
    pub http: Arc<Client>,
    pub command: Box<ApplicationCommand>,
}

impl CommandContext {
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

    /// Gets all of the User options with a given option name.
    pub fn all_users(&self, name: &'static str) -> impl Iterator<Item = UserId> + '_ {
        self.all_options_named(name)
            .filter_map(|opt| match opt.value {
                CommandOptionValue::User(user) => Some(user),
                _ => None,
            })
    }

    /// Gets all of the String options with a given option name.
    pub fn all_strings(&self, name: &'static str) -> impl Iterator<Item = &str> + '_ {
        self.all_options_named(name)
            .filter_map(|opt| match opt.value {
                CommandOptionValue::String(ref value) => Some(value.as_ref()),
                _ => None,
            })
    }

    /// Attempts to find the first argument with a given name that is of type Integer. If no such
    /// argument is found, return None.
    pub fn get_string(&self, name: &'static str) -> InteractionResult<&String> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::String(ref value) => Some(value),
                _ => None,
            })
            .ok_or(InteractionError::MissingArgument(name))
    }

    /// Attempts to find the first argument with a given name that is of type Integer. If no such
    /// argument is found, return None.
    pub fn get_int(&self, name: &'static str) -> InteractionResult<i64> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::Integer(ref value) => Some(*value),
                _ => None,
            })
            .ok_or(InteractionError::MissingArgument(name))
    }

    /// Attempts to find the first argument with a given name that is of type User. If no such
    /// argument is found, return None.
    pub fn get_user(&self, name: &'static str) -> InteractionResult<UserId> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::User(ref value) => Some(*value),
                _ => None,
            })
            .ok_or(InteractionError::MissingArgument(name))
    }

    /// Attempts to find the first argument with a given name that is of type Channel. If no such
    /// argument is found, return None.
    pub fn get_channel(&self, name: &'static str) -> InteractionResult<ChannelId> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::Channel(ref value) => Some(*value),
                _ => None,
            })
            .ok_or(InteractionError::MissingArgument(name))
    }

    /// Attempts to find the first argument with a given name that is of type Role. If no such
    /// argument is found, return None.
    pub fn get_role(&self, name: &'static str) -> InteractionResult<RoleId> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::Role(ref value) => Some(*value),
                _ => None,
            })
            .ok_or(InteractionError::MissingArgument(name))
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

impl InteractionContext for CommandContext {
    fn http(&self) -> &Arc<Client> {
        &self.http
    }

    fn id(&self) -> InteractionId {
        self.command.id
    }

    fn application_id(&self) -> ApplicationId {
        self.command.application_id
    }

    fn token(&self) -> &str {
        &self.command.token
    }

    fn member(&self) -> Option<&PartialMember> {
        self.command.member.as_ref()
    }

    fn guild_id(&self) -> InteractionResult<GuildId> {
        self.command.guild_id.ok_or(InteractionError::NotInGuild)
    }

    fn channel_id(&self) -> ChannelId {
        self.command.channel_id
    }

    fn user(&self) -> &User {
        let member = self
            .command
            .member
            .as_ref()
            .and_then(|member| member.user.as_ref());
        let user = self.command.user.as_ref();
        user.or(member).unwrap()
    }
}
