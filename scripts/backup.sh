#!/bin/bash

# Script for backing up the databases for Hourai

tmp_dir=$(mktemp -d -t hourai-backup-XXXXXXXXXX)

now=$(date +%Y-%m-%d"_"%H_%M_%S)
archive=$(pwd)/dump_$now.tar.gz

psql_dump="$tmp_dir/dump.psql"
redis_dump="$tmp_dir/dump.rdb"

docker exec postgres pg_dumpall -c -U hourai > $psql_dump
docker cp redis:/data/dump.rdb $redis_dump

cd $tmp_dir
tar -czvf $archive *
rm -rf $tmp_dir
