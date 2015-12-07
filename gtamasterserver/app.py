import flask
import threading
from datetime import datetime
from json import dumps
from os import environ
from waitress import serve
from time import sleep

app = flask.Flask(__name__)

servers = {}

@app.route('/', methods=['GET', 'POST'])
def index():
	global servers	
	if flask.request.method == 'POST':
		dejson = {'port': flask.request.data}
		print dejson['port']
		srvString = flask.request.remote_addr + ':' + dejson['port']
		servers[srvString] = datetime.now()
		return '200'
	else:
		return dumps({"list":servers.keys()})


def checkThread():
	print 'cleaning list...'
	for server in dict(servers):
		date = servers[server]
		if (datetime.now() - date).total_seconds() > 10*60:
			del servers[server]

	sleep(10*60)
	checkThread()


if __name__ == '__main__':
	t = threading.Thread(target=checkThread)
	t.daemon = True
	t.start()

	app.debug = True #InDev ONLY 
	#serve(app, port=int(environ['PORT'])) #For deployment
	app.run() #Run our app. #InDev ONLY