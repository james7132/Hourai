import collections
from . import models
from datetime import datetime, timedelta
from hourai.db import proto


EscalationResult = collections.namedtuple(
    'EscalationResult',
    "current_level entry current_rung next_rung expiration")


def get_rung(level, config):
    if level < 0:
        return None
    idx = min(level, len(config.escalation_ladder.rung) - 1)
    return config.escalation_ladder.rung[idx]


class EscalationException(BaseException):

    def __init__(self, message):
        self.message = message


class UserEscalationHistory:

    def __init__(self, bot, user, guild, session=None):
        self.bot = bot
        self.session = session or bot.create_storage_session()
        self.user_id = user.id
        self.guild = guild
        self.guild_proxy = bot.get_guild_proxy(guild)

        self.entries = list(self.__query_history())

    @property
    def current_level(self):
        level = -1
        for entry in self.entries:
            level = max(-1, level + entry.level_delta)
        return level

    def escalate(self, authorizer, reason):
        """|coro|

        Escalates a user and applies the corresponding action from the
        escalation ladder.

        Returns an EscalationResult.
        """
        return self.apply_diff(authorizer, reason, 1, execute=True)

    def deescalate(self, authorizer, reason):
        """|coro|

        Deescalates a user.

        Returns an EscalationResult.
        """
        return self.apply_diff(authorizer, reason, -1, execute=False)

    async def apply_diff(self, authorizer, reason, diff, execute=False):
        """|coro|

        Sets the level of a given user. Cannot be set to a negative value.
        """
        config = await self.guild_proxy.config.get('moderation')
        if reason is None or len(reason) <= 0:
            raise EscalationException('A reason MUST be provided.')
        elif len(config.escalation_ladder.rung) <= 0:
            raise EscalationException(
                'No escalation ladder has been configured.')

        level = max(-1, self.current_level + diff)
        new_rung = get_rung(level, config)

        actions = proto.ActionSet()
        if execute:
            for rung_action in new_rung.action:
                action = actions.action.add()
                action.CopyFrom(rung_action)
                self.__setup_action(action, reason)
                await self.bot.action_manager.execute(action)
        else:
            action = actions.action.add()
            action.escalate.amount = diff
            self.__setup_action(action, reason)

        entry = self.__create_entry(authorizer, new_rung, diff)
        entry.action = actions
        self.session.add(entry)

        expiration = self.__schedule_deescalation(new_rung, entry)

        self.session.commit()

        result = EscalationResult(
            entry=entry, current_rung=new_rung,
            next_rung=get_rung(level + 1, config),
            current_level=level, expiration=expiration)
        await self.__send_modlog_result(result, diff)
        return result

    async def __send_modlog_result(self, result, diff):
        try:
            reasons = set(a.reason for a in result.entry.action.action)
            modlog = await self.guild_proxy.get_modlog()
            await modlog.send(''.join([
                ':arrow_up:' if diff > 0 else ':arrow_down:',
                f'**<@{result.entry.authorizer_id}> ',
                'escalated' if diff > 0 else 'deescalated',
                f' <@{result.entry.subject_id}>**\n',
                f'Reason: {"; ".join(reasons)}\n',
                f'Action: {result.entry.display_name}\n',
                f'Expiration: {result.expiration or "Never"}'
            ]))
        except Exception as error:
            await self.bot.send_owner_error(error)

    def __schedule_deescalation(self, rung, entry):
        expiration = None
        deesc = self.session.query(models.PendingDeescalation) \
                            .get((self.user_id, self.guild.id))
        # Schedule Deescalation only if it can
        if rung is not None and rung.HasField('deescalation_period'):
            #  This will update existing deescalation entries and add ones that
            #  don't already exist
            expiration = entry.timestamp + \
                timedelta(seconds=rung.deescalation_period)

            deesc = deesc or models.PendingDeescalation(
                user_id=self.user_id,
                guild_id=self.guild.id
            )
            deesc.amount = -1
            deesc.expiration = expiration
            deesc.entry = entry

            self.session.merge(deesc)
        elif deesc is not None:
            # Remove any pending deescalation if there doesn't need to be one
            self.session.delete(deesc)

        return expiration

    def __create_entry(self, authorizer, rung, level_delta):
        authorizer_name = f"{authorizer.name}#{authorizer.discriminator}",
        display_name = (rung.display_name if level_delta >= 0 else
                        'Deescalate')
        return models.EscalationEntry(
            guild_id=self.guild.id,
            subject_id=self.user_id,
            authorizer_id=authorizer.id,
            authorizer_name=authorizer_name,
            display_name=display_name,
            timestamp=datetime.utcnow(),
            level_delta=level_delta)

    def __setup_action(self, action, reason):
        action.user_id = self.user_id
        action.guild_id = self.guild.id
        action.reason = reason

    def __query_history(self):
        return self.session.query(models.EscalationEntry) \
                           .filter_by(guild_id=self.guild.id,
                                      subject_id=self.user_id) \
                           .order_by(models.EscalationEntry.timestamp) \
                           .all()
