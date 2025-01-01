build:
	dotnet build

run:
	dotnet run .\api\participant-api.csproj

test:
	dotnet test

redis:
	docker run --name redis -p 6379:6379 -d redis
