# syntax=docker/dockerfile:1

# Etape 1 : Builder — installe les dépendances Python.
# Toutes les dépendances ont des wheels précompilées (vérifié avec
# `uv pip install --no-build`) : pas besoin de gcc ni de paquets apt.
FROM python:3.12-slim AS builder
RUN --mount=type=cache,target=/root/.cache/pip,sharing=locked pip install uv
COPY requirements.txt .
# Le cache uv survit aux builds : modifier requirements.txt ne retélécharge
# que les paquets nouveaux ou changés. --no-build échoue si un paquet
# devait être compilé (au lieu d'exiger gcc en silence).
ENV UV_LINK_MODE=copy
RUN --mount=type=cache,target=/root/.cache/uv,sharing=locked \
    uv pip install --system --no-build --prefix=/install -r requirements.txt

# Etape 2 : Production — Python + dépendances + code, rien d'autre.
FROM python:3.12-slim AS production
WORKDIR /app
# Sécurité : utilisateur non-privilégié (créé avant le code pour garder ce layer en cache)
RUN useradd -m nextstep
COPY --from=builder /install /usr/local
COPY --chown=nextstep:nextstep . .
USER nextstep

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
