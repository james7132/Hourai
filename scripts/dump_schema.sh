#!/bin/bash

# Script for backing up the Postgres schema for Hourai

docker exec postgres pg_dump -C -s -U hourai | sed -e '/^--/d' -e '/^$/d' > schema.psql
