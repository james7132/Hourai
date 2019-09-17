FROM python:3.7-alpine3.8 as base

FROM base as builder
RUN apk update && apk add --upgrade git protobuf gcc musl-dev
COPY requirements.txt /
RUN pip install --prefix /install -r /requirements.txt
WORKDIR /app
COPY . /app
RUN protoc $(find . -type f -regex ".*\.proto") --python_out=.

FROM base
COPY --from=builder /install /usr/local
COPY --from=builder /app /app
WORKDIR /app
CMD ["python", "-m", "hourai"]
