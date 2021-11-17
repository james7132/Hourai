use crate::Client;
use anyhow::Result;
use hourai::models::channel::Message;

const DEPRECATION_RESPONSE: &str = "
This command has been deprecated and implemented as a slash command. Plase use `/music` instead.
For more information, please see <https://docs.hourai.gg/Slash-Commands>.
";

pub async fn on_message_create(client: Client<'static>, evt: Message) -> Result<()> {
    if evt.author.bot {
        return Ok(());
    }

    if client.parser.parse(evt.content.as_str()).is_some() {
        client
            .http_client
            .create_message(evt.channel_id)
            .content(DEPRECATION_RESPONSE)?
            .exec()
            .await?;
    }

    Ok(())
}
