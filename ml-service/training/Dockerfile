FROM python:3.10-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /workspace/ml-service

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg libgomp1 \
    && rm -rf /var/lib/apt/lists/*

COPY training/requirements.txt /tmp/training-requirements.txt
RUN python -m pip install --no-cache-dir -r /tmp/training-requirements.txt

COPY . .

CMD ["python", "training/train_head.py", "--help"]
