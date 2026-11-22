# .NET 11 supports Debian 13 (trixie) and not Debian 12 (bookworm).
ARG VARIANT="trixie"
FROM buildpack-deps:${VARIANT}-curl


ENV \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_ROOT=/usr/share/dotnet/ \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=false\
    DOTNET_USE_POLLING_FILE_WATCHER=true


# [Optional] Uncomment this section to install additional OS packages.
# RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
#     && apt-get -y install --no-install-recommends <your-package-list-here>
