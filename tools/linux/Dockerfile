FROM ubuntu:22.04@sha256:b1066385161d28ddf6bc7e7b28a9170eec11484c821d1a5150d176cbde41d7f7
ENV DEBIAN_FRONTEND=noninteractive PYTHONUNBUFFERED=1
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 python3-venv python3-dev python3-pip python3-gi python3-gi-cairo \
    gir1.2-gtk-3.0 gir1.2-webkit2-4.1 gcc pkg-config libcairo2-dev \
    libgirepository1.0-dev binutils xvfb xauth dbus-x11 libgl1-mesa-dri \
    fonts-dejavu-core ca-certificates && rm -rf /var/lib/apt/lists/*
RUN python3 -m venv --system-site-packages /opt/buildenv
RUN apt-get update && apt-get install -y --no-install-recommends python3-evdev && rm -rf /var/lib/apt/lists/*
ENV PATH=/opt/buildenv/bin:$PATH
COPY requirements.txt /tmp/requirements-linux.txt
RUN pip install --no-cache-dir -r /tmp/requirements-linux.txt
WORKDIR /work
