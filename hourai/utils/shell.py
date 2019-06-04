# For running shell commands from Discord and reading the output

import asyncio
import time
from utils import embed as embed_util
from utils import format

SHELL_UPDATE_FREQUENCY = 5

async def run_command(ctx, cmd):
    proc = await asyncio.create_subprocess_shell(cmd,
                                                 stdout=asyncio.subprocess.PIPE,
                                                 stderr=asyncio.subprocess.PIPE)
    last_update = 0
    stdout = []
    response = ""
    await ctx.edit(content="Running {}".format(format.simple_code(cmd)))
    embed = embed_util.text_to_embed('')
    def _update_embed():
        embed.description = format.ellipsize(format.multiline_code(''.join(stdout)),
                                             keep_end=True)
    while True:
        stdout_line = await proc.stdout.readline()
        if stdout_line:
             stdout.append(stdout_line.decode())
        else:
            break
        diff = time.time() - last_update
        if diff < SHELL_UPDATE_FREQUENCY:
            continue
        _update_embed()
        await ctx.edit(content="Running {}:".format(format.simple_code(cmd)),
                       embed=embed)
        last_update = time.time()
    _update_embed()
    output = '{} exited with code {}:'.format(format.simple_code(cmd), proc.returncode)
    await ctx.message.edit(content=output, embed=embed)
    return proc.returncode
