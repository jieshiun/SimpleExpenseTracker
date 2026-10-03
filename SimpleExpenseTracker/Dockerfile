FROM node:24-bookworm-slim AS frontend
WORKDIR /web
RUN npm install -g pnpm@11
COPY frontend/simple-expense-tracker-web/package.json frontend/simple-expense-tracker-web/pnpm-lock.yaml ./
COPY frontend/simple-expense-tracker-web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/simple-expense-tracker-web/ ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
COPY --from=frontend /web/dist ./frontend/simple-expense-tracker-web/dist
RUN dotnet publish backend/SimpleExpenseTracker.Api \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data && chown app:app /data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SimpleExpenseTracker.Api.dll"]
