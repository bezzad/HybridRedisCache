'use strict';

import { htmlReport } from "https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js";
import { textSummary } from "https://jslib.k6.io/k6-summary/0.0.1/index.js";
import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { randomString, randomIntBetween, uuidv4 } from './helper.js';
const BASE_URL = "https://localhost:7037/WeatherForecast";
const myTrend = new Trend('waiting_time');
const Max_SleepTime_Second = 20;

console.log('Start HybridCache Load Test with Grafano/K6 instance');

export const options = {
    vus: 20, // virtual users (VUs)
    executor: 'ramping-arrival-rate',
    preAllocatedVUs: 10,
    timeUnit: '1s',
    startRate: 5,
    // Ramp the number of virtual users up and down
    stages: [
        { target: 500, duration: '10s' },  // linearly go from 10 iters/s to 500 iters/s for 10s
        { target: 500, duration: '10s' },  // continue with 500 iters/s for 10s
        { target: 1000, duration: '60s' }, // linearly go from 500 iters/s to 1000 iters/s for 60s
        { target: 1000, duration: '30s' }, // continue with 1000 iters/s for 30s
        { target: 1500, duration: '60s' }, // linearly go from 1000 iters/s to 1500 iters/s for 60s
        { target: 1500, duration: '30s' }, // continue with 1500 iters/s for 30s
        { target: 2000, duration: '60s' }, // linearly go from 1500 iters/s to 2000 iters/s for 60s
        { target: 2000, duration: '30s' }, // continue with 2000 iters/s for 30s
        { target: 3000, duration: '0' },   // immediate to 3000 iters/s
        { target: 3000, duration: '30s' }, // continue with 3000 iters/s for 2 minutes
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

    let payload = {
        id: uuidv4(),
        date: new Date(),
        temperatureC: randomIntBetween(1, 40),
        summary: `Name ${randomString(10)}`
    }

    let newPayload = {
        id: payload.id,
        date: payload.date,
        temperatureC: 22,
        summary: "new summery"
    };

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

    group('Create a weather', () => {
        let res = http.post(BASE_URL, JSON.stringify(payload), requestConfigWithTag({ name: 'Create' }));
        myTrend.add(res.timings.waiting);
        if (!check(res, { 'Weather created correctly': (r) => r.status === 200 })) {
            console.error(`Unable to create a Weather ${res.status} ${res.body}`);
        }
    });

    sleep(Math.random() * Max_SleepTime_Second); // Duration, in seconds.

    group('GET with 10 aggregate requests', () => {
        let responses = http.batch([
            ['GET', `${BASE_URL}/${payload.id}`, requestConfigWithTag({ name: 'GET' })],
            ['GET', `${BASE_URL}/${payload.id}`, requestConfigWithTag({ name: 'GET' })],
            ['GET', `${BASE_URL}/${payload.id}`, requestConfigWithTag({ name: 'GET' })],
            ['GET', `${BASE_URL}/${payload.id}`, requestConfigWithTag({ name: 'GET' })],
            ['GET', `${BASE_URL}/${payload.id}`, requestConfigWithTag({ name: 'GET' })],
        ]);

        const temperatures = Object.values(responses).map((res) => res.json('temperatureC'));
        if (!check(temperatures, {
            'Weathers temperatures are more than 1°C': Math.min(...temperatures) >= 1,
            'Weathers temperatures are less than 40°C': Math.max(...temperatures) <= 40,
        })) {
            console.error(`Unable to get the Weather(id: ${payload.id})! ${res.status} ${res.body}`);
        };
    });
    sleep(Math.random() * Max_SleepTime_Second); // Duration, in seconds.

    group('Update weather', () => {
        let res = http.post(BASE_URL, JSON.stringify(newPayload), requestConfigWithTag({ name: 'Update' }));
        myTrend.add(res.timings.waiting);
        if (!check(res, { 'Update worked': (r) => r.status == 200 })) {
            console.error(`Unable to update the weather(id: ${newPayload.id})! ${res.status} ${res.body}`);
        }
    });
    sleep(Math.random() * Max_SleepTime_Second); // Duration, in seconds.

    group('Read and verify updated data', () => {
        let res = http.get(`${BASE_URL}/${newPayload.id}`, requestConfigWithTag({ name: 'Read and Verify' }));
        myTrend.add(res.timings.waiting);
        if (!check(res, {
            'Update worked': (r) => r.status === 200,
            'Updated name is correct': (r) => JSON.parse(r.body).summary == newPayload.summary,
            'Updated temp is correct': (r) => JSON.parse(r.body).temperatureC == newPayload.temperatureC,
        })) {
            console.error(`Verify update the weather(id: ${newPayload.id}) was unsuccess! ${res.status} ${res.body}`);
        }
    });
    sleep(Math.random() * Max_SleepTime_Second); // Duration, in seconds.

    group('Delete weather', () => {
        let res = http.del(`${BASE_URL}/${newPayload.id}`, null, requestConfigWithTag({ name: 'Delete' }));
        myTrend.add(res.timings.waiting);
        if (!check(res, { 'Weather was deleted correctly': () => res.status === 200 })) {
            console.error(`Weather(id: ${newPayload.id}) was not deleted properly! ${res.status} ${res.body}`);
        }
    });
    sleep(Math.random() * Max_SleepTime_Second); // Duration, in seconds.
}

export function handleSummary(data) {
    return {
        // stdout: latency_message,
        'summary.json': JSON.stringify(data), //the default data object
        "result.html": htmlReport(data),
        stdout: textSummary(data, { indent: " ", enableColors: true }),
    };
}