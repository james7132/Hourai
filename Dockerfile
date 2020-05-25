FROM python:3.8-alpine as base

FROM base as builder
RUN apk add --no-cache \
  protobuf gcc make libffi-dev build-base postgresql-dev \
  && mkdir /app
COPY requirements.txt /
RUN pip install --prefix /install -r /requirements.txt
COPY . /app
RUN protoc $(find /app -type f -regex ".*\.proto") \
  --proto_path=/app \
  --python_out=/app

FROM base
WORKDIR /app
RUN apk add --no-cache libstdc++ postgresql-dev
# Run as non-root user
RUN addgroup -g 969 -S hourai && \
    adduser -u 969 -S hourai -G hourai
USER hourai
COPY --from=builder /install /usr/local
COPY --from=builder /app /app
CMD ["python", "launcher.py", "run"]
