FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

RUN mkdir /data
COPY Amintiri.Api/bin/Release/netcoreapp3.1/linux-x64/publish/ app/
WORKDIR app
ENTRYPOINT ["/usr/bin/dotnet", "Amintiri.dll"]
