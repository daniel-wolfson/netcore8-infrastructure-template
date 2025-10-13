#!/usr/bin/env sh
set -eu

# Fallback AWS CLI wrapper: prefer awslocal, else aws with --endpoint-url
LOCALSTACK_ENDPOINT=${LOCALSTACK_ENDPOINT:-http://localhost:4566}
aws_local() {
  if command -v awslocal >/dev/null 2>&1; then
    awslocal "$@"
  elif command -v aws >/dev/null 2>&1; then
    aws --endpoint-url="$LOCALSTACK_ENDPOINT" "$@"
  else
    echo "ERROR: Neither 'awslocal' nor 'aws' CLI found in container PATH" >&2
    exit 127
  fi
}

printf "================================================\n"
echo "| Initializing Amazon SQS Queues"
printf "================================================\n"

# Wait for LocalStack to be fully ready and CLI to be usable
echo "? Waiting for LocalStack/CLI to be ready..."
for i in 1 2 3 4 5; do
  if aws_local sqs list-queues >/dev/null 2>&1; then
    echo "OK: AWS CLI is ready" >&2
    break
  fi
  sleep 2
  if [ "$i" -eq 5 ]; then
    echo "ERROR: AWS CLI not ready after retries" >&2
    exit 1
  fi
done

echo ""
echo "?? Creating SQS Queues..."

# Create standard queues for learning
echo "  ? Creating test-orders-queue..."
aws_local sqs create-queue \
  --queue-name test-orders-queue \
  --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600,ReceiveMessageWaitTimeSeconds=20 || true

echo "  ? Creating test-orders-queue-dlq (Dead Letter Queue)..."
aws_local sqs create-queue \
  --queue-name test-orders-queue-dlq \
  --attributes MessageRetentionPeriod=1209600 || true
  iod=1209600 || true

echo "  ? Creating test-notifications-queue..."
aws_local sqs create-queue \
  --queue-name test-notifications-queue \
  --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600 || true

echo "  ? Creating test-jobs-queue..."
aws_local sqs create-queue \
  --queue-name test-jobs-queue \
  --attributes VisibilityTimeout=60,DelaySeconds=0 || true

# Get queue URLs
echo ""
echo "?? Getting Queue URLs..."
ORDERS_QUEUE_URL=$(aws_local sqs get-queue-url --queue-name test-orders-queue --output text)
DLQ_URL=$(aws_local sqs get-queue-url --queue-name test-orders-queue-dlq --output text)
NOTIFICATIONS_QUEUE_URL=$(aws_local sqs get-queue-url --queue-name test-notifications-queue --output text)
JOBS_QUEUE_URL=$(aws_local sqs get-queue-url --queue-name test-jobs-queue --output text)

# Get DLQ ARN
DLQ_ARN=$(aws_local sqs get-queue-attributes \
  --queue-url "$DLQ_URL" \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' \
  --output text)

# Configure Dead Letter Queue for main orders queue
echo ""
echo "??  Configuring Dead Letter Queue..."
REDRIVE=$(printf '{"deadLetterTargetArn":"%s","maxReceiveCount":"3"}' "$DLQ_ARN")
aws_local sqs set-queue-attributes \
  --queue-url "$ORDERS_QUEUE_URL" \
  --attributes RedrivePolicy="$REDRIVE" || true

echo "  ? Redrive policy set: max 3 retries before sending to DLQ"

# Create a FIFO queue for learning
echo ""
echo "?? Creating FIFO Queue (for ordered message learning)..."
aws_local sqs create-queue \
  --queue-name test-orders-queue.fifo \
  --attributes FifoQueue=true,ContentBasedDeduplication=true || true

echo ""
echo "?? SQS Queues Created:"
aws_local sqs list-queues || true

echo ""
echo "? Queue Attributes:"
echo "---------------------------------------------------"
for queue in test-orders-queue test-orders-queue-dlq test-notifications-queue test-jobs-queue test-orders-queue.fifo; do
  echo ""
  echo "?? Queue: $queue"
  QUEUE_URL=$(aws_local sqs get-queue-url --queue-name "$queue" --output text)
  aws_local sqs get-queue-attributes \
    --queue-url "$QUEUE_URL" \
    --attribute-names All \
    --output table || true
done

echo ""
printf "================================================\n"
echo "? Amazon SQS Setup Complete!"
printf "================================================\n"
