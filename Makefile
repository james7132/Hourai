build:
	docker-compose -p hourai -f config/docker-compose.build.yml build

pull:
	docker-compose -p hourai -f config/docker-compose.prod.yml pull

start_dev:
	docker-compose -p hourai -f config/docker-compose.dev.yml up -d --build

start_prod:
	docker-compose -p hourai -f config/docker-compose.prod.yml up -d

stop_dev:
	docker-compose -p hourai -f config/docker-compose.dev.yml down

stop_prod:
	docker-compose -p hourai -f config/docker-compose.prod.yml down

clean:
	yes | docker system prune
