'use strict';

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { randomString, randomIntBetween, uuidv4 } from './helper.js';
const BASE_URL = "https://localhost:7037/WeatherForecast";
const myTrend = new Trend('waiting_time');

console.log('Start HybridCache Load Test with Grafano/K6 instance');

export const options = {
    vus: 20, // virtual users (VUs)
    executor: 'ramping-arrival-rate',
    preAllocatedVUs: 60,
    timeUnit: '1s',
    startRate: 50,
    // Ramp the number of virtual users up and down
    stages: [
        { target: 200, duration: '30s' }, // linearly go from 50 iters/s to 200 iters/s for 30s
        { target: 500, duration: '0' }, // instantly jump to 500 iters/s
        { target: 500, duration: '1m' }, // continue with 500 iters/s for 1 minute
    ],
    thresholds: {
        // Assert that 99% of requests finish within 3000ms.
        http_req_duration: ["p(99) < 3000"],
    },
    noConnectionReuse: true,
    userAgent: 'HybridRedisCache_K6_LoadTest/1.0',
};


// Simulated user behavior
export default function (authToken) {
    var requestConfigWithTag = (tag) => ({
        'headers': {
            //Authorization: `Bearer ${authToken}`,
            'Content-Type': 'application/json'
        },
        'tag': Object.assign(
            {},
            {
                name: 'PrivateWeathers',
            },
            tag
        ),
    });

    var getSampleWeather = () => ({
        id: uuidv4(),
        date: Date.UTC,
        temperatureC: randomIntBetween(1, 40),
        summary: `Name ${randomString(10)}`
    });

    group('Create and modify weathers', () => {
        let payload = getSampleWeather();
        let newPayload = getSampleWeather();
        newPayload.id = payload.id;

        group('Create weathers', () => {
            let res = http.post(BASE_URL, JSON.stringify(payload), requestConfigWithTag({ name: 'Create' }));
            myTrend.add(res.timings.waiting);
            if (!check(res, { 'Weather created correctly': (r) => r.status === 200 })) {
                console.error(`Unable to create a Weather ${res.status} ${res.body}`);
            }
        });

        group('GET with multiple requests', () => {
            let responses = http.batch([
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
                ['GET', `${BASE_URL}/${payload.id}`, null, requestConfigWithTag({ name: 'GET' })],
            ]);

            const temperatures = Object.values(responses).map((res) => res.json('temperatureC'));

            // Functional test
            if (!check(temperatures, {
                'Weathers temperatures are more than 1°C': Math.min(...temperatures) >= 1,
                'Weathers temperatures are less than 40°C': Math.max(...temperatures) <= 40,
            })) {
                console.error(`Unable to get the Weather(id: ${payload.id}): ${res.status} ${res.body}`);
            };
        });

        group('Update weather', () => {
            let res = http.post(BASE_URL, JSON.stringify(newPayload), requestConfigWithTag({ name: 'Update' }));
            myTrend.add(res.timings.waiting);
            if (!check(res, { 'Update worked': (r) => r.status == 200 })) {
                console.error(`Unable to update the weather(id: ${newPayload.id}): ${res.status} ${res.body}`);
            }
        });

        group('Read and verify updated data', () => {
            let res = http.get(`${BASE_URL}/${newPayload.id}`, requestConfigWithTag({ name: 'Read and Verify' }));
            myTrend.add(res.timings.waiting);
            if (!check(res, {
                'Update worked': (r) => r.status === 200,
                'Updated name is correct': (r) => JSON.parse(r.body).summary == newPayload.summary,
                'Updated temp is correct': (r) => JSON.parse(r.body).temperatureC == newPayload.temperatureC,
            })) {
                console.error(`Verify update the weather(id: ${newPayload.id}) was unsuccess! ${res.status} ${res.body}`);
                console.error(res);
            }
        });

        group('Delete weather', () => {
            let res = http.del(`${BASE_URL}/${newPayload.id}`, null, requestConfigWithTag({ name: 'Delete' }));
            myTrend.add(res.timings.waiting);
            if (!check(res, { 'Weather was deleted correctly': () => res.status === 200 })) {
                console.error(`Weather(id: ${newPayload.id}) was not deleted properly`);
            }
        });
    });

    sleep(1);
}