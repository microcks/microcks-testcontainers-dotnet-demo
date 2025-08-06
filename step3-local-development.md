# Step 3: Local development experience with Microcks

Our application uses Kafka and external dependencies.

Currently, if you run the application from your terminal, you will see the following error:

```shell
dotnet run  

Génération...
Unhandled exception. System.InvalidOperationException: Section 'PastryApi' not found in configuration.
   at Microsoft.Extensions.Configuration.ConfigurationExtensions.GetRequiredSection(IConfiguration configuration, String key)
   at Program.<Main>$(String[] args) in /Users/sebastiendegodez/Documents/source.nosync/microcks-testcontainers-dotnet-demo/src/Order.Service/Program.cs:line 31
```

To run the application locally, we need to have a Kafka broker up and running + the other dependencies corresponding to our Pastry API provider and reviewing system.

Instead of installing these services on our local machine, or using Docker to run these services manually, we will use a utility tool with this simple command microcks.sh. Microcks docker-compose file (microcks-docker-compose.yml) has been configured to automatically import the Order API contract but also the Pastry API contracts. Both APIs are discovered on startup and Microcks UI should be available on http://localhost:9090 in your browser:

```shell
./microcks.sh
[+] Running 4/4
 ✔ Container microcks-kafka                                  Started                                              0.1s 
 ✔ Container microcks-testcontainers-dotnet-demo-microcks-1  Started                                              0.1s 
 ✔ Container microcks-async-minion                           Started                                              0.1s 
 ✔ Container microcks-testcontainers-dotnet-demo-importer-1  Started                                              0.1s 
```

Because our `Order Service` application has been configured to talk to Microcks mocks (see the default settings in `appsettings.Development.json` and `launchSettings.json`), you should be able to directly call the `Order API` and invoke the whole chain made of the 3 components:

```shell
curl -XPOST localhost:5088/api/orders -H 'Content-type: application/json' \
    -d '{"customerId": "lbroudoux", "productQuantities": [{"productName": "Millefeuille", "quantity": 1}], "totalPrice": 10.1}'
==== OUTPUT ====
{"id":"4b06233e-48d0-4477-86f9-a88d92634bbe","status":0,"customerId":"lbroudoux","productQuantities":[{"productName":"Millefeuille","quantity":1}],"totalPrice":10.1}% 
```

# Play with the API

Terminal:

```shell
curl -POST localhost:5088/api/orders -H 'Content-type: application/json' \ 
    -d '{"customerId": "lbroudoux", "productQuantities": [{"productName": "Millefeuille", "quantity": 1}], "totalPrice": 5.1}' -v
```

You should get a response similar to the following:

```shell
* Host localhost:5088 was resolved.
* IPv6: ::1
* IPv4: 127.0.0.1
*   Trying [::1]:5088...
* Connected to localhost (::1) port 5088
> POST /api/orders HTTP/1.1
> Host: localhost:5088
> User-Agent: curl/8.7.1
> Accept: */*
> Content-type: application/json
> Content-Length: 117
> 
* upload completely sent off: 117 bytes
< HTTP/1.1 201 Created
< Content-Type: application/json; charset=utf-8
< Date: Mon, 04 Aug 2025 22:50:50 GMT
< Server: Kestrel
< Location: /api/orders/ee8af10a-9740-4870-a23b-0aaa7705fdee
< Transfer-Encoding: chunked
< 
* Connection #0 to host localhost left intact
{"id":"ee8af10a-9740-4870-a23b-0aaa7705fdee","status":0,"customerId":"lbroudoux","productQuantities":[{"productName":"Millefeuille","quantity":1}],"totalPrice":5.1}%
```

Now test with something else, requesting for another Pastry:

```shell
curl -POST localhost:5088/api/orders -H 'Content-type: application/json' \
    -d '{"customerId": "lbroudoux", "productQuantities": [{"productName": "Eclair Chocolat", "quantity": 1}], "totalPrice": 4.1}' -v
```

This time you get another "exception" response:

```shell
* Host localhost:5088 was resolved.
* IPv6: ::1
* IPv4: 127.0.0.1
*   Trying [::1]:5088...
* Connected to localhost (::1) port 5088
> POST /api/orders HTTP/1.1
> Host: localhost:5088
> User-Agent: curl/8.7.1
> Accept: */*
> Content-type: application/json
> Content-Length: 120
> 
* upload completely sent off: 120 bytes
< HTTP/1.1 422 Unprocessable Entity
< Content-Type: application/json; charset=utf-8
< Date: Mon, 04 Aug 2025 22:54:57 GMT
< Server: Kestrel
< Transfer-Encoding: chunked
< 
* Connection #0 to host localhost left intact
{"productName":"Eclair Chocolat","details":"Pastry 'Eclair Chocolat' is unavailable."}%
```

and this is because Microcks has created different simulations for the Pastry API 3rd party API based on API artifacts we loaded. Check the `tests/resources/third-parties/apipastries-openapi.yml` and `tests/resources/third-parties/apipastries-postman-collection.json` files to get details.


### 
[Next](step4-write-rest-tests.md)
