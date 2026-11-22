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

# The install script downloads the latest release from github.com/starship/starship/releases and makes no GitHub API call.
# The pipe exits 0 when curl gets no script, so starship --version fails the build instead.
RUN curl -sS https://starship.rs/install.sh | sh -s -- --yes && starship --version
