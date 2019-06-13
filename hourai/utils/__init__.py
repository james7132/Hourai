from hourai import config

async def success(ctx, suffix=None):
    if suffix:
        await ctx.send(f'{config.SUCCESS_RESPONSE}: {suffix}')
    else:
        await ctx.send(config.SUCCESS_RESPONSE)
