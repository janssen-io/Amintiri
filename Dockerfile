FROM mcr.microsoft.com/dotnet/core/aspnet:3.0

RUN mkdir /data
COPY Amintiri.Api/bin/Release/netcoreapp3.0/linux-x64/publish/ app/
WORKDIR app
ENTRYPOINT ["/usr/bin/dotnet", "Amintiri.dll"]
