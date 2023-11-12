use crate::{
    http::Client,
    interactions::{InteractionContext, InteractionError, InteractionResult},
    models::{
        application::interaction::{
            application_command::{
                CommandData, CommandDataOption, CommandOptionValue, InteractionMember,
            },
            Interaction, InteractionData,
        },
        guild::{PartialMember, Permissions},
        id::{
            marker::{
                ApplicationMarker, ChannelMarker, GuildMarker, InteractionMarker, RoleMarker,
                UserMarker,
            },
            Id,
        },
        user::User,
    },
};
use std::marker::PhantomData;
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
    pub command: Interaction,
    marker_: PhantomData<()>,
}

impl CommandContext {
    pub fn new(client: Arc<Client>, interaction: Interaction) -> Self {
        assert!(matches!(
            interaction.data,
            Some(InteractionData::ApplicationCommand(_))
        ));
        Self {
            http: client,
            command: interaction,
            marker_: PhantomData,
        }
    }

    fn data(&self) -> &CommandData {
        match &self.command.data {
            Some(InteractionData::ApplicationCommand(data)) => data,
            _ => panic!("Provided interaction data is not an application command"),
        }
    }

    pub fn command(&self) -> Command<'_> {
        let data = self.data();
        let base = data.name.as_ref();
        if let Some((sub, options)) = Self::get_subcommand(&data.options) {
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
        Self::flatten_options(&self.data().options).into_iter()
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
    pub fn all_users(&self, name: &'static str) -> impl Iterator<Item = Id<UserMarker>> + '_ {
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
    pub fn get_user(&self, name: &'static str) -> InteractionResult<Id<UserMarker>> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::User(ref value) => Some(*value),
                _ => None,
            })
            .ok_or(InteractionError::MissingArgument(name))
    }

    /// Attempts to find the first argument with a given name that is of type Channel. If no such
    /// argument is found, return None.
    pub fn get_channel(&self, name: &'static str) -> InteractionResult<Id<ChannelMarker>> {
        self.option_named(name)
            .and_then(|option| match option.value {
                CommandOptionValue::Channel(ref value) => Some(*value),
                _ => None,
            })
            .ok_or(InteractionError::MissingArgument(name))
    }

    /// Attempts to find the first argument with a given name that is of type Role. If no such
    /// argument is found, return None.
    pub fn get_role(&self, name: &'static str) -> InteractionResult<Id<RoleMarker>> {
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

    pub fn resolve_user(&self, id: Id<UserMarker>) -> Option<&User> {
        self.data().resolved.as_ref().and_then(|r| r.users.get(&id))
    }

    pub fn resolve_member(&self, id: Id<UserMarker>) -> Option<&InteractionMember> {
        self.data()
            .resolved
            .as_ref()
            .and_then(|r| r.members.get(&id))
    }

    fn flatten_options(options: &[CommandDataOption]) -> Vec<&CommandDataOption> {
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

    fn get_subcommand(
        options: &[CommandDataOption],
    ) -> Option<(&str, &Vec<CommandDataOption>)> {
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

    fn id(&self) -> Id<InteractionMarker> {
        self.command.id
    }

    fn application_id(&self) -> Id<ApplicationMarker> {
        self.command.application_id
    }

    fn token(&self) -> &str {
        &self.command.token
    }

    fn member(&self) -> Option<&PartialMember> {
        self.command.member.as_ref()
    }

    fn guild_id(&self) -> InteractionResult<Id<GuildMarker>> {
        self.command.guild_id.ok_or(InteractionError::NotInGuild)
    }

    fn channel_id(&self) -> Id<ChannelMarker> {
        self.command.channel_id.unwrap()
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
