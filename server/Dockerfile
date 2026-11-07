FROM ubuntu:24.04
RUN apt-get update \
    && apt-get install -y --no-install-recommends nut-server nut-client python3 \
    && rm -rf /var/lib/apt/lists/*
COPY nut/ /etc/nut/
COPY start-nut.sh /usr/local/bin/start-ups-nut
COPY web/ /opt/ups-web/
ENV PYTHONUNBUFFERED=1
