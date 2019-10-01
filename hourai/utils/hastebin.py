async def post(session, content: str, base_url='https://hasteb.in'):
    endpoint = '{}/documents'.format(base_url)
    async with session.post(endpoint, data=content.encode('utf-8')) as request:
        return '{}/{}'.format(base_url, (await request.json())['key'])
