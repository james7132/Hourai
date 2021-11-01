#[macro_use]
extern crate lazy_static;

mod approvers;
mod context;
mod rejectors;
mod verifier;

use hourai::config;
use std::time::Duration;
use verifier::BoxedVerifier;

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());
    let sql = hourai_sql::init(&config).await;
    let verifiers: Vec<BoxedVerifier> = vec![
        // ---------------------------------------------------------------
        // Suspicion Level Verifiers
        //     Verifiers here are mostly for suspicious characteristics.
        //     These are designed with a high-recall, low precision methdology.
        //     False positives from these are more likely.  These are low severity
        //     checks.
        // -----------------------------------------------------------------

        // New user accounts are commonly used for alts of banned users.
        rejectors::new_account(chrono::Duration::days(30)),
        // Low effort user bots and alt accounts tend not to set an avatar.
        rejectors::no_avatar(),
        // Deleted accounts shouldn't be able to join new servers. A user
        // joining that is seemingly deleted is suspicious.
        rejectors::deleted_user(sql.clone()),

        // Filter likely user bots based on usernames.
        //rejectors::StringFilterRejector(
            //prefix='Likely user bot. ',
            //filters=load_list('user_bot_names')),
        //rejectors::StringFilterRejector(
            //prefix='Likely user bot. ',
            //full_match=True,
            //filters=load_list('user_bot_names_fullmatch')),

        // If a user has Nitro, they probably aren't an alt or user bot.
        approvers::nitro(),

        // -----------------------------------------------------------------
        // Questionable Level Verifiers
        //     Verifiers here are mostly for red flags of unruly or
        //     potentially troublesome.  These are designed with a
        //     high-recall, high-precision methdology. False positives from
        //     these are more likely to occur.
        // -----------------------------------------------------------------

        // Filter usernames and nicknames that match moderator users.
        //rejectors.NameMatchRejector(
            //prefix='Username matches moderator\'s. ',
            //filter_func=utils.is_moderator,
            //min_match_length=4),
        //rejectors.NameMatchRejector(
            //prefix='Username matches moderator\'s. ',
            //filter_func=utils.is_moderator,
            //member_selector=lambda m: m.nick,
            //min_match_length=4),

        // Filter usernames and nicknames that match bot users.
        //rejectors.NameMatchRejector(
            //prefix='Username matches bot\'s. ',
            //filter_func=lambda m: m.bot,
            //min_match_length=4),
        //rejectors.NameMatchRejector(
            //prefix='Username matches bot\'s. ',
            //filter_func=lambda m: m.bot,
            //member_selector=lambda m: m.nick,
            //min_match_length=4),

        // Filter offensive usernames.
        //rejectors.StringFilterRejector(
            //prefix='Offensive username. ',
            //filters=load_list('offensive_usernames')),

        // Filter sexually inapproriate usernames.
        //rejectors.StringFilterRejector(
            //prefix='Sexually inapproriate username. ',
            //filters=load_list('sexually_inappropriate_usernames')),

        // Filter potentially long usernames that use wide unicode characters that
        // may be disruptive or spammy to other members.
        // TODO(james7132): Reenable wide unicode character filter

        // -----------------------------------------------------------------
        // Malicious Level Verifiers
        //     Verifiers here are mostly for known offenders.
        //     These are designed with a low-recall, high precision
        //     methdology. False positives from these are far less likely to
        //     occur.
        // -----------------------------------------------------------------

        // Make sure the user is not banned on other servers.
        rejectors::banned_user(sql.clone(), /* min_guild_size */ 150),

        // Check the username against known banned users from the current
        // server. Requires exact username match (case insensitive)
        rejectors::banned_username(sql.clone()),

        // Check if the user is distinguished (Discord Staff, Verified, Partnered,
        // etc).
        //approvers::distinguished_user(),

        // All non-override users are rejected while guilds are locked down.
        //rejectors.LockdownRejector(),

        // -----------------------------------------------------------------
        // Override Level Verifiers
        //     Verifiers here are made to explictly override previous
        //     verifiers. These are specifically targetted at a small
        //     specific group of individiuals. False positives and negatives
        //     at this level are very unlikely if not impossible.
        // -----------------------------------------------------------------
        approvers::bot(),
        approvers::bot_owners(vec![]),
    ];
}
