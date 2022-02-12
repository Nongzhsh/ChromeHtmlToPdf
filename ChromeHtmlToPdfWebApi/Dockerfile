#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2.8 AS base
# Suppress an apt-key warning about standard out not being a terminal. Use in this script is safe.
ENV APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=DontWarn

# export DEBIAN_FRONTEND="noninteractive"
ENV DEBIAN_FRONTEND noninteractive

# Install deps + add Chrome Stable + purge all the things
RUN apt-get update && apt-get install -y \
	apt-transport-https \
	ca-certificates \
	curl \
	gnupg \
	--no-install-recommends \
	&& curl -sSL https://dl.google.com/linux/linux_signing_key.pub | apt-key add - \
	&& echo "deb [arch=amd64] https://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list \
	&& apt-get update && apt-get install -y \
	google-chrome-stable \
	--no-install-recommends \
	&& apt-get purge --auto-remove -y curl gnupg \
	&& rm -rf /var/lib/apt/lists/*

# Chrome Driver
RUN apt-get update && \
    apt-get install -y unzip && \
    wget https://chromedriver.storage.googleapis.com/2.31/chromedriver_linux64.zip && \
    unzip chromedriver_linux64.zip && \
    mv chromedriver /usr/bin && rm -f chromedriver_linux64.zip

# Install MS core fonts
RUN sed -i'.bak' 's/$/ contrib/' /etc/apt/sources.list
RUN apt-get update; apt-get install -y ttf-mscorefonts-installer fontconfig

# Install custom fonts
COPY /fonts /usr/share/fonts
RUN fc-cache -f -v

WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:2.2.402 AS build
WORKDIR /src
COPY ["ChromeHtmlToPdfWebApi/ChromeHtmlToPdfWebApi.csproj", "ChromeHtmlToPdfWebApi/"]
COPY ["ChromeHtmlToPdfLib/ChromeHtmlToPdfLib.csproj", "ChromeHtmlToPdfLib/"]
RUN dotnet restore "ChromeHtmlToPdfWebApi/ChromeHtmlToPdfWebApi.csproj"
COPY . .
WORKDIR "/src/ChromeHtmlToPdfWebApi"
RUN dotnet build "ChromeHtmlToPdfWebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChromeHtmlToPdfWebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChromeHtmlToPdfWebApi.dll"]