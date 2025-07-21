# Step 1: Getting Started

Before getting started, let's make sure you have everything you need for running this demo.

## Prerequisites

### Install .NET SDK
You'll need the latest .NET SDK for this workshop. Testcontainers libraries are compatible with .NET, and this workshop uses an ASP.NET Core application.

We recommend downloading the latest .NET SDK from the [official .NET website](https://dotnet.microsoft.com/download).

### Install Docker
You need to have a [Docker](https://docs.docker.com/get-docker/) or [Podman](https://podman.io/) environment to use Testcontainers.

```bash
$ docker version

Client:
 Version:           28.1.1
 API version:       1.49
 Go version:        go1.23.8
 Git commit:        4eba377
 Built:             Fri Apr 18 09:49:45 2025
 OS/Arch:           darwin/arm64
 Context:           desktop-linux

Server: Docker Desktop 4.41.2 (191736)
 Engine:
  Version:          28.1.1
  API version:      1.49 (minimum version 1.24)
  Go version:       go1.23.8
  Git commit:       01f442b
  Built:            Fri Apr 18 09:52:08 2025
  OS/Arch:          linux/arm64
  Experimental:     false
 containerd:
  Version:          1.7.27
  GitCommit:        05044ec0a9a75232cad458027ca83437aae3f4da
 runc:
  Version:          1.2.5
  GitCommit:        v1.2.5-0-g59923ef
 docker-init:
  Version:          0.19.0
  GitCommit:        de40ad0
```

## Download the project

Clone the [microcks-testcontainers-dotnet-demo](https://github.com/microcks/microcks-testcontainers-dotnet-demo) repository from GitHub to your computer:

```bash
git clone https://github.com/microcks/microcks-testcontainers-dotnet-demo.git
```

## Compile the project to download the dependencies

With .NET CLI:

```bash
dotnet restore
dotnet build
```
