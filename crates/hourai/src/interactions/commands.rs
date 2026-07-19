use crate::{
    http::Client,
    interactions::{InteractionContext, InteractionError, InteractionResult},
    models::{
        application::interaction::{
            Interaction, InteractionData, InteractionMember,
            application_command::{CommandData, CommandDataOption, CommandOptionValue},
        },
        guild::{PartialMember, Permissions},
        id::{
            Id,
            marker::{
                ApplicationMarker, ChannelMarker, GuildMarker, InteractionMarker, RoleMarker,
                UserMarker,
            },
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

    #[expect(clippy::expect_used)]
    fn data(&self) -> &CommandData {
        match &self.command.data {
            Some(InteractionData::ApplicationCommand(data)) => data,
            _ => Option::<&CommandData>::None
                .expect("Provided interaction data is not an application command"),
        }
    }

    pub fn command<'a>(&'a self) -> Command<'a> {
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
    pub fn options(&self) -> FlattenedOptions<'_> {
        FlattenedOptions::new(&self.data().options)
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

    fn get_subcommand(options: &[CommandDataOption]) -> Option<(&str, &[CommandDataOption])> {
        for option in options.iter() {
            if let CommandOptionValue::SubCommand(ref options) = option.value {
                return Some((option.name.as_ref(), options));
            }
        }
        None
    }
}

/// Iterator that flattens nested Discord command options without heap allocations.
///
/// Discord application commands enforce a maximum nesting depth of 3 levels:
/// (Command -> Subcommand Group -> Subcommand -> Option). A fixed-size array stack of 3
/// elements allows traversing all valid command options entirely on the stack.
pub struct FlattenedOptions<'a> {
    stack: [Option<&'a [CommandDataOption]>; 3],
    indices: [usize; 3],
    depth: usize,
}

impl<'a> FlattenedOptions<'a> {
    fn new(options: &'a [CommandDataOption]) -> Self {
        Self {
            stack: [Some(options), None, None],
            indices: [0, 0, 0],
            depth: 0,
        }
    }
}

impl<'a> Iterator for FlattenedOptions<'a> {
    type Item = &'a CommandDataOption;

    fn next(&mut self) -> Option<Self::Item> {
        loop {
            let slice = self.stack[self.depth]?;
            if self.indices[self.depth] >= slice.len() {
                if self.depth == 0 {
                    return None;
                }
                self.stack[self.depth] = None;
                self.indices[self.depth] = 0;
                self.depth -= 1;
                self.indices[self.depth] += 1;
                continue;
            }

            let opt = &slice[self.indices[self.depth]];
            match &opt.value {
                CommandOptionValue::SubCommand(sub) | CommandOptionValue::SubCommandGroup(sub) => {
                    if self.depth + 1 < self.stack.len() {
                        self.depth += 1;
                        self.stack[self.depth] = Some(sub.as_slice());
                        self.indices[self.depth] = 0;
                        continue;
                    }
                }
                _ => {}
            }

            self.indices[self.depth] += 1;
            return Some(opt);
        }
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

    #[expect(clippy::expect_used)]
    fn channel_id(&self) -> Id<ChannelMarker> {
        self.command
            .channel
            .as_ref()
            .map(|c| c.id)
            .expect("Interaction is missing channel")
    }

    #[expect(clippy::expect_used)]
    fn user(&self) -> &User {
        let member = self
            .command
            .member
            .as_ref()
            .and_then(|member| member.user.as_ref());
        let user = self.command.user.as_ref();
        user.or(member)
            .expect("Interaction has neither user nor member")
    }
}
