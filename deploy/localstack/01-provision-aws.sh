#!/bin/bash
# Executado pelo LocalStack quando o container fica pronto (hook ready.d).
# A aplicacao tambem cria esses recursos de forma idempotente no startup;
# provisionar aqui deixa o ambiente pronto antes mesmo da API subir.
set -euo pipefail

REGION="${AWS_DEFAULT_REGION:-us-east-1}"
ENDPOINT="http://localhost:4566"

echo "[localstack-init] criando tabela DynamoDB doacoes"
awslocal dynamodb create-table \
  --table-name doacoes \
  --attribute-definitions \
      AttributeName=CampanhaId,AttributeType=S \
      AttributeName=DoacaoId,AttributeType=S \
      AttributeName=DoadorId,AttributeType=S \
      AttributeName=CriadaEm,AttributeType=S \
  --key-schema \
      AttributeName=CampanhaId,KeyType=HASH \
      AttributeName=DoacaoId,KeyType=RANGE \
  --global-secondary-indexes \
      'IndexName=doador-index,KeySchema=[{AttributeName=DoadorId,KeyType=HASH},{AttributeName=CriadaEm,KeyType=RANGE}],Projection={ProjectionType=ALL}' \
  --billing-mode PAY_PER_REQUEST \
  --region "$REGION" >/dev/null 2>&1 || echo "[localstack-init] tabela ja existe"

echo "[localstack-init] criando SNS topic e filas SQS (provider Aws opcional)"
TOPIC_ARN=$(awslocal sns create-topic --name doacoes-recebidas --region "$REGION" --output text --query TopicArn)
DLQ_URL=$(awslocal sqs create-queue --queue-name doacoes-recebidas-dlq --region "$REGION" --output text --query QueueUrl)
DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url "$DLQ_URL" --attribute-names QueueArn --region "$REGION" --output text --query 'Attributes.QueueArn')

QUEUE_URL=$(awslocal sqs create-queue \
  --queue-name doacoes-recebidas \
  --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$DLQ_ARN\\\",\\\"maxReceiveCount\\\":\\\"5\\\"}\",\"VisibilityTimeout\":\"60\"}" \
  --region "$REGION" --output text --query QueueUrl)
QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url "$QUEUE_URL" --attribute-names QueueArn --region "$REGION" --output text --query 'Attributes.QueueArn')

awslocal sns subscribe \
  --topic-arn "$TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$QUEUE_ARN" \
  --attributes RawMessageDelivery=true \
  --region "$REGION" >/dev/null

echo "[localstack-init] recursos AWS provisionados: $TOPIC_ARN / $QUEUE_URL"
