import discord
from hourai.data.actions_pb2 import Action
from hourai.data.models_pb2 import MemberId, DiscordMessage

def create_action(ctx):
    action = Action()

    action.source.CopyFrom(create_action_source(ctx))

    return action

def create_action_source(ctx):
    source = ActionSource()
    source.authorizer_id.append(ctx.author.id)
    source.timestamp.GetCurrentTime()
    source.command.message.CopyFrom(to_message_proto(ctx.message))
    return context

def to_message_proto(msg):
    channel = msg.channel

    message = DiscordMessage()
    message.id = msg.id
    message.channel_id = channel.id
    if hasattr('guild', channel):
        message.guild_id = channel.guild.id
    message.content = msg.content
    message.created_at.FromDateTime(msg.created_at)
    return message

def id_to_proto(member: discord.Member):
    return MemberId(user_id=member.id,
                    guild_id=member.guild.id)
