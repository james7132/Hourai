use anyhow::Result;
use hourai::{
    http::Client,
    models::{
        application::{
            callback::{CallbackData, InteractionResponse},
            interaction::application_command::{ApplicationCommand, CommandDataOption},
        },
        channel::{embed::Embed, message::MessageFlags},
        guild::PartialMember,
        id::GuildId,
        user::User,
    },
};

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
    pub http: Client,
    pub command: Box<ApplicationCommand>,
}

impl CommandContext {
    pub fn guild_id(&self) -> Option<GuildId> {
        self.command.guild_id
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
            .interaction_callback(self.command.id, self.command.token.clone(), response)
            .await?;
        Ok(())
    }

    pub async fn update(&self, content: String) -> Result<()> {
        self.http
            .update_interaction_original(self.command.token.clone())?
            .content(Some(content.into()))?
            .await?;
        Ok(())
    }

    fn get_subcommand<'a>(
        options: &'a Vec<CommandDataOption>,
    ) -> Option<(&'a str, &'a Vec<CommandDataOption>)> {
        for option in options.iter() {
            if let CommandDataOption::SubCommand { name, options } = option {
                return Some((name.as_ref(), options));
            }
        }
        None
    }
}
