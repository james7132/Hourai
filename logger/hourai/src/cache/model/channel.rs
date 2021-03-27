use twilight_model::id::ChannelId;

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CachedChannel {
    pub id: ChannelId,
    pub name: Box<str>,
}
