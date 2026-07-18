use anyhow::Result;
use futures::{channel::mpsc, stream::StreamExt};
use hourai::models::{
    gateway::payload::{
        incoming::MemberChunk, outgoing::request_guild_members::RequestGuildMembers,
    },
    id::{marker::GuildMarker, Id},
};
use std::{collections::VecDeque, sync::Arc};
use twilight_gateway::MessageSender;

enum Message {
    Guild(Id<GuildMarker>),
    GuildChunk {
        guild_id: Id<GuildMarker>,
        chunk_count: u32,
        chunk_index: u32,
    },
}

#[derive(Clone)]
pub struct MemberChunker(mpsc::UnboundedSender<Message>);

impl MemberChunker {
    pub fn new(senders: Arc<Vec<MessageSender>>) -> Self {
        let (tx, rx) = mpsc::unbounded();
        tokio::spawn(Self::run(rx, senders));
        Self(tx)
    }

    pub fn push_guild(&self, guild_id: Id<GuildMarker>) {
        self.0.unbounded_send(Message::Guild(guild_id)).ok();
    }

    pub fn push_chunk(&self, chunk: &MemberChunk) {
        self.0
            .unbounded_send(Message::GuildChunk {
                guild_id: chunk.guild_id,
                chunk_count: chunk.chunk_count,
                chunk_index: chunk.chunk_index,
            })
            .ok();
    }

    async fn run(mut rx: mpsc::UnboundedReceiver<Message>, senders: Arc<Vec<MessageSender>>) {
        let mut count = 0;
        let mut current = None;
        let mut queue = VecDeque::new();
        while let Some(msg) = rx.next().await {
            let chunk = match msg {
                Message::Guild(guild_id) => {
                    if current.is_some() {
                        if !queue.contains(&guild_id) {
                            queue.push_back(guild_id);
                        }
                        current
                    } else {
                        Some(guild_id)
                    }
                }
                Message::GuildChunk {
                    guild_id,
                    chunk_count,
                    chunk_index,
                } => {
                    tracing::debug!(
                        "Recieved chunk {} of {} for guild {}",
                        chunk_index,
                        chunk_count,
                        guild_id
                    );
                    count += 1;
                    if chunk_count == count {
                        count = 0;
                        queue.pop_front()
                    } else {
                        current
                    }
                }
            };

            if current == chunk {
                continue;
            }

            if let Some(ref guild) = chunk {
                Self::chunk_guild(&senders, *guild).await.unwrap();
            }

            current = chunk;
        }
    }

    async fn chunk_guild(
        senders: &Arc<Vec<MessageSender>>,
        guild_id: Id<GuildMarker>,
    ) -> Result<()> {
        tracing::debug!("Chunking guild: {}", guild_id);
        let request = RequestGuildMembers::builder(guild_id)
            .presences(true)
            .query(String::new(), None);
        let shard_id = (guild_id.get() >> 22) % (senders.len() as u64);
        if let Some(sender) = senders.get(shard_id as usize) {
            sender.command(&request)?;
        }
        Ok(())
    }
}
