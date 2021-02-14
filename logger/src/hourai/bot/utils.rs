//use serenity::framework::standard::Args;
//use crate::bot;
//use twilight_model::id::*;
//use twilight_model::Member;
//use std::collections::HashSet;
//use serenity::model::prelude::*;

//pub struct MemberQuery {
    //guild_id: GuildId,
    //user_ids: HashSet<UserId>,
    //usernames: HashSet<String>,
    //cached: HashSet<Member>
//}

//impl MemberQuery {
    //pub fn new(guild_id: GuildId) -> Self {
        //return Self {
            //guild_id: guild_id,
            //user_ids: HashSet::new(),
            //usernames: HashSet::new(),
            //cached: HashSet<Member>::new()
        //};
    //}

    //pub fn len(&self) -> usize {
        //return self.user_ids.len() + self.usernames.len();
    //}

    ///// Fetches full member models from Discord. If only member IDs are needed, use
    ///// fetch_members_ids instead.
    //pub async fn fetch_members(&mut self, http: impl AsRef<serenity::Http>) -> serenity::Result<Vec<Member>> {
    //}

    ///// Fetches full member models from Discord. If full member models are needed, use
    ///// fetch_members.
    //pub async fn fetch_members_ids(&mut self, http: impl AsRef<serenity::http::Http>) -> serenity::Result<Vec<Member>> {
    //}

    //async fn query_by_id(&mut self, http: &bot::Client) {
        //let nonce =
        //self.user_ids.clear();
    //}

    //async fn query_by_names(&mut self, http: &serenity::http::Http) {
        //self.user_ids.clear();
    //}

    //pub fn add_parameter(&mut self, param: impl AsRef<str>) -> Result<(), &'static str> {
        //let parameter = param.as_ref().trim();
        //if let Ok(user_id) = parameter.parse::<u64>() {
            //self.user_ids.insert(UserId::from(user_id));
            //return Ok(());
        //}
        //if let Some(user_id) = serenity::utils::parse_username(&parameter) {
            //self.user_ids.insert(UserId::from(user_id));
            //return Ok(());
        //}
        //let components: Vec<&str> = parameter.split("#").collect();
        //if components.len() == 2 && components[1].len() == 4 {
            //if let Ok(_) = components[1].parse::<u32>() {
                //self.usernames.insert(String::from(parameter));
            //}
        //}
        //return Err("Failed to parse the parameter.");
    //}

    //pub fn parse_args(&mut self, args: &mut Args) {
        //while !args.is_empty() {
            //if let Err(_) = args.single::<String>().map(|param| self.add_parameter(param)) {
                //// If it fails it went over by one.
                //args.rewind();
                //break;
            //}
        //}
    //}
//}
