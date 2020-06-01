build:
	docker-compose -f config/docker-compose.prod.yml build

start_common:
	docker-compose -p hourai-shared -f config/docker-compose.common.yml up -d

start_dev: start_common
	docker-compose -p hourai-prod -f config/docker-compose.prod.yml up -d

start_prod: start_common
	docker-compose -p hourai-prod -f config/docker-compose.prod.yml up -d

stop_dev:
	docker-compose -p hourai-prod -f config/docker-compose.prod.yml down

stop_prod:
	docker-compose -p hourai-prod -f config/docker-compose.prod.yml down

stop_all: stop_dev stop_prod
	docker-compose -p hourai-shared -f config/docker-compose.common.yml down

clean:
	yes | docker system prune
