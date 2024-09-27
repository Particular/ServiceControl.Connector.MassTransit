pushd

cd NServiceBus.AmazonSQS

git fetch origin
git reset --hard origin/tf527

cd ../NServiceBus.RabbitMQ

git fetch origin
git reset --hard origin/tf527

cd ../NServiceBus.Transport.AzureServiceBus

git fetch origin
git reset --hard origin/tf527

popd