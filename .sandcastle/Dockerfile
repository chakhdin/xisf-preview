FROM node:22-bookworm

# Install system dependencies
RUN apt-get update && apt-get install -y \
  git \
  curl \
  jq \
  && rm -rf /var/lib/apt/lists/*

# Issue tracker: local markdown files under .scratch/, driven by
# .sandcastle/issues.mjs (plain Node, no extra CLI needed).

# .NET 8 SDK builds net48 against the reference assemblies; Mono (with libgdiplus
# for System.Drawing) runs the tests through the xunit console runner.
RUN apt-get update && apt-get install -y libicu72 mono-complete libgdiplus unzip \
  && rm -rf /var/lib/apt/lists/* \
  && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
  && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet \
  && ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet \
  && rm /tmp/dotnet-install.sh \
  && curl -fsSL https://www.nuget.org/api/v2/package/xunit.runner.console/2.9.2 -o /tmp/xunit.zip \
  && unzip -q /tmp/xunit.zip 'tools/net48/*' -d /opt/xunit && rm /tmp/xunit.zip \
  && chmod -R a+rX /opt/xunit
ENV DOTNET_ROOT=/usr/share/dotnet \
  DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  DOTNET_NOLOGO=1 \
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  XUNIT_CONSOLE=/opt/xunit/tools/net48/xunit.console.exe

# Build-args for UID/GID alignment: sandcastle docker build-image
# defaults these to the host user's UID/GID so image-built files
# and bind-mounted files share an owner without runtime chown.
ARG AGENT_UID=1000
ARG AGENT_GID=1000

# Rename the base image's "node" user to "agent" and align UID/GID.
RUN groupmod -o -g $AGENT_GID node && usermod -o -u $AGENT_UID -g $AGENT_GID -d /home/agent -m -l agent node
USER ${AGENT_UID}:${AGENT_GID}

# Install Claude Code CLI
RUN curl -fsSL https://claude.ai/install.sh | bash

# Add Claude to PATH
ENV PATH="/home/agent/.local/bin:$PATH"

WORKDIR /home/agent

# In worktree sandbox mode, Sandcastle bind-mounts the git worktree at /home/agent/workspace
# and overrides the working directory to /home/agent/workspace at container start.
# Structure your Dockerfile so that /home/agent/workspace can serve as the project root.
ENTRYPOINT ["sleep", "infinity"]
