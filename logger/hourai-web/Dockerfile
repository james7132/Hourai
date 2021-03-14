FROM ekidd/rust-musl-builder:stable as build
ENV DOCKER_BUILD=1
COPY logger/ .
COPY proto/ ./proto/
RUN cargo build --release --bins

# Copy the statically-linked binary into a scratch container.
FROM alpine
RUN apk --update --no-cache add jsonnet && \
    mkdir /etc/hourai && \
    chown 969:969 /etc/hourai
USER 969

ENV RUST_LOG=info
ENV HOURAI_CONFIG=/opt/hourai.jsonnet
ENV HOURAI_ENV=dev

COPY --from=build /home/rust/src/target/x86_64-unknown-linux-musl/release/hourai-web .
CMD /usr/bin/jsonnet -m /etc/hourai $HOURAI_CONFIG && /hourai-web
