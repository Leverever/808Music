import json
import logging
import time
from typing import Any, Callable, TypeVar

import pika

from app.config import Settings
from app.domain import StemSeparationJob

logger = logging.getLogger(__name__)
TJob = TypeVar("TJob")


class RabbitMqConsumer:
    def __init__(
        self,
        settings: Settings,
        handle_job: Callable[[TJob], None],
        parse_message: Callable[[dict[str, Any]], TJob] = StemSeparationJob.from_message,
    ) -> None:
        self._settings = settings
        self._handle_job = handle_job
        self._parse_message = parse_message

    def start(self) -> None:
        while True:
            try:
                self._consume()
            except pika.exceptions.AMQPConnectionError:
                logger.exception("RabbitMQ connection failed; retrying shortly")
                time.sleep(5)

    def _consume(self) -> None:
        credentials = pika.PlainCredentials(
            self._settings.rabbitmq_username,
            self._settings.rabbitmq_password,
        )
        parameters = pika.ConnectionParameters(
            host=self._settings.rabbitmq_host,
            port=self._settings.rabbitmq_port,
            virtual_host=self._settings.rabbitmq_virtual_host,
            credentials=credentials,
            heartbeat=self._settings.rabbitmq_heartbeat,
            blocked_connection_timeout=300,
        )

        connection = pika.BlockingConnection(parameters)
        channel = connection.channel()

        channel.exchange_declare(
            exchange=self._settings.rabbitmq_exchange,
            exchange_type="topic",
            durable=True,
        )
        channel.queue_declare(queue=self._settings.rabbitmq_queue, durable=True)
        channel.queue_bind(
            exchange=self._settings.rabbitmq_exchange,
            queue=self._settings.rabbitmq_queue,
            routing_key=self._settings.rabbitmq_routing_key,
        )
        channel.basic_qos(prefetch_count=self._settings.rabbitmq_prefetch_count)

        def on_message(channel, method, properties, body: bytes) -> None:
            try:
                payload = json.loads(body.decode("utf-8"))
                job = self._parse_message(payload)
                self._handle_job(job)
                channel.basic_ack(delivery_tag=method.delivery_tag)
            except Exception:
                logger.exception("Failed to process RabbitMQ message")
                channel.basic_nack(delivery_tag=method.delivery_tag, requeue=True)

        channel.basic_consume(
            queue=self._settings.rabbitmq_queue,
            on_message_callback=on_message,
        )

        logger.info("Consuming RabbitMQ queue=%s", self._settings.rabbitmq_queue)
        channel.start_consuming()
