FROM alpine
RUN apk --update --no-cache add jsonnet && \
    mkdir /etc/hourai && \
    chown 969:969 /etc/hourai
USER 969

ENV RUST_LOG=info
ENV HOURAI_CONFIG=/opt/hourai.jsonnet
ENV HOURAI_ENV=dev

COPY target/x86_64-unknown-linux-musl/release/hourai-web .
CMD /usr/bin/jsonnet -m /etc/hourai $HOURAI_CONFIG && /hourai-web
