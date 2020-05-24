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
        self.user = user
        self.guild = user.guild
        self.guild_proxy = bot.create_guild_proxy(user.guild)

        self.entries = list(self.__query_history())

    @property
    def current_level(self):
        level = -1
        for entry in self.entries:
            level = max(0, level + entry.level_delta)
        return level

    async def escalate(self, authorizer, reason):
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
        config = await self.guild_proxy.get_config('moderation')
        if reason is None or len(reason) <= 0:
            raise EscalationException('A reason MUST be provided.')
        elif len(config.escalation_ladder.rung) <= 0:
            raise EscalationException(
                'No escalation ladder has been configured.')

        level = max(-1, self.current_level + diff)
        new_rung = get_rung(level, config)

        action = proto.Action()
        if execute:
            action.CopyFrom(new_rung.action)
            self.__setup_action(action, reason)
            await self.bot.action_manager.execute(action)
        else:
            action.escalate.amount = diff
            self.__setup_action(action, reason)

        entry = self.__create_entry(authorizer, new_rung, diff)
        entry.action = action
        self.session.add(entry)

        expiration = self.__schedule_escalation(new_rung, entry)

        self.session.commit()

        return EscalationResult(
            entry=entry, current_rung=new_rung,
            next_rung=get_rung(level + 1),
            current_level=level, expiration=expiration)

    def __schedule_deescalation(self, rung, entry):
        expiration = None
        # Schedule Deescalation only if it can
        if rung is not None and rung.HasField('deescalation_period'):
            #  This will update existing deescalation entries and add ones that
            #  don't already exist
            expiration = entry.timestamp + \
                timedelta(rung.deescalation_period)
            self.session.add(models.PendingDeescalation(
                user_id=self.user.id, guild_id=self.guild.id,
                expiration=expiration, amount=-1,
                entryg=entry
            ))
        else:
            # Remove any pending deescalation if there doesn't need to be one
            self.session.query(models.PendingDeescalation) \
                        .filter_by(user_id=self.user.id,
                                   guild_id=self.guild_id) \
                        .delete()
        return expiration

    def __create_entry(self, authorizer, rung, level_delta):
        authorizer_name = f"{authorizer.name}#{authorizer.discriminator}",
        return models.EscalationEntry(
            guild_id=self.guild.id,
            subject_id=self.user.id,
            authorizer_id=authorizer.id,
            authorizer_name=authorizer_name,
            display_name=rung.display_name,
            timestamp=datetime.utcnow(),
            level_delta=level_delta)

    def __setup_action(self, action, reason):
        action.user_id = self.user.id
        action.guild_id = self.guild.id
        action.reason = reason

    def __query_history(self):
        return self.session.query(models.EscalationEntry) \
                           .filter_by(guild_id=self.guild.id,
                                      subject_id=self.user.id) \
                           .order_by(models.EscalationEntry.timestamp) \
                           .all()
