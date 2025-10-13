#!/bin/bash

echo "================================================"
echo "?? Initializing Amazon SQS Queues"
echo "================================================"

# Wait for LocalStack to be fully ready
echo "? Waiting for LocalStack to be ready..."
awslocal sqs list-queues || sleep 5

echo ""
echo "?? Creating SQS Queues..."

# Create standard queues for learning
echo "  ? Creating test-orders-queue..."
awslocal sqs create-queue \
    --queue-name test-orders-queue \
    --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600,ReceiveMessageWaitTimeSeconds=20

echo "  ? Creating test-orders-queue-dlq (Dead Letter Queue)..."
awslocal sqs create-queue \
    --queue-name test-orders-queue-dlq \
    --attributes MessageRetentionPeriod=1209600

echo "  ? Creating test-notifications-queue..."
awslocal sqs create-queue \
    --queue-name test-notifications-queue \
    --attributes VisibilityTimeout=30,MessageRetentionPeriod=345600

echo "  ? Creating test-jobs-queue..."
awslocal sqs create-queue \
    --queue-name test-jobs-queue \
    --attributes VisibilityTimeout=60,DelaySeconds=0

# Get queue URLs
echo ""
echo "?? Getting Queue URLs..."
ORDERS_QUEUE_URL=$(awslocal sqs get-queue-url --queue-name test-orders-queue --output text)
DLQ_URL=$(awslocal sqs get-queue-url --queue-name test-orders-queue-dlq --output text)
NOTIFICATIONS_QUEUE_URL=$(awslocal sqs get-queue-url --queue-name test-notifications-queue --output text)
JOBS_QUEUE_URL=$(awslocal sqs get-queue-url --queue-name test-jobs-queue --output text)

# Get DLQ ARN
DLQ_ARN=$(awslocal sqs get-queue-attributes \
    --queue-url "$DLQ_URL" \
    --attribute-names QueueArn \
    --query 'Attributes.QueueArn' \
    --output text)

# Configure Dead Letter Queue for main orders queue
echo ""
echo "??  Configuring Dead Letter Queue..."
awslocal sqs set-queue-attributes \
    --queue-url "$ORDERS_QUEUE_URL" \
    --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$DLQ_ARN\\\",\\\"maxReceiveCount\\\":\\\"3\\\"}\"}"

echo "  ? Redrive policy set: max 3 retries before sending to DLQ"

# Create a FIFO queue for learning
echo ""
echo "?? Creating FIFO Queue (for ordered message learning)..."
awslocal sqs create-queue \
    --queue-name test-orders-queue.fifo \
    --attributes "FifoQueue=true,ContentBasedDeduplication=true"

echo ""
echo "?? SQS Queues Created:"
awslocal sqs list-queues

echo ""
echo "? Queue Attributes:"
echo "??????????????????????????????????????????????"

for queue in test-orders-queue test-orders-queue-dlq test-notifications-queue test-jobs-queue test-orders-queue.fifo; do
    echo ""
    echo "?? Queue: $queue"
    QUEUE_URL=$(awslocal sqs get-queue-url --queue-name "$queue" --output text)
    awslocal sqs get-queue-attributes \
        --queue-url "$QUEUE_URL" \
        --attribute-names All \
        --output table || true
done

echo ""
echo "================================================"
echo "? Amazon SQS Setup Complete!"
echo "================================================"
echo ""
echo "?? Quick Start Commands:"
echo "??????????????????????????????????????????????"
echo ""
echo "# List all queues:"
echo "  awslocal sqs list-queues"
echo ""
echo "# Send a test message:"
echo "  awslocal sqs send-message \\"
echo "    --queue-url http://localhost:4566/000000000000/test-orders-queue \\"
echo "    --message-body 'Hello from SQS!'"
echo ""
echo "# Receive messages:"
echo "  awslocal sqs receive-message \\"
echo "    --queue-url http://localhost:4566/000000000000/test-orders-queue \\"
echo "    --max-number-of-messages 10 \\"
echo "    --wait-time-seconds 5"
echo ""
echo "# Check queue attributes:"
echo "  awslocal sqs get-queue-attributes \\"
echo "    --queue-url http://localhost:4566/000000000000/test-orders-queue \\"
echo "    --attribute-names All"
echo ""
echo "# Purge queue (remove all messages):"
echo "  awslocal sqs purge-queue \\"
echo "    --queue-url http://localhost:4566/000000000000/test-orders-queue"
echo ""
echo "?? Run .NET Tests:"
echo "  dotnet test --filter 'FullyQualifiedName~AmazonSqsTests'"
echo ""
echo "================================================"
