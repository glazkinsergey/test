from telethon import TelegramClient
import time

#loop = asyncio.get_event_loop()

api_id = 441946
api_hash = '927ac3db15d4f533830abd64efabe261'
client = TelegramClient('anon', api_id, api_hash)
nnn = 1

async def main():
    iii = 1000
    async for message in client.iter_messages('dprpetrovka'):
        if 'MessageActionChatAddUser' in str(message.action):
        	await client.delete_messages('dprpetrovka', message.id)
        iii -= 1
        if iii == 0:
        	break
#    await asyncio.sleep(60)

with client:
    while True:
        client.loop.run_until_complete(main())
        print(nnn, ' ')
        nnn += 1
        time.sleep(60)

#loop.run_until_complete(main())
