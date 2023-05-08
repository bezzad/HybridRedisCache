'use strict';

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';
const myTrend = new Trend('waiting_time');

console.log('HybridCache Load Test with Grafano/K6');

export const options = {
    vus: 20, // virtual users (VUs)
    // Ramp the number of virtual users up and down
    stages: [
        { duration: '10s', target: 20 },
        { duration: '10s', target: 10 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        // Assert that 99% of requests finish within 3000ms.
        http_req_duration: ["p(99) < 3000"],
    },
    noConnectionReuse: true,
    userAgent: 'MyK6UserAgentString/1.0',
};

// Simulated user behavior
export default function () {
    const res = http.get('https://localhost:7037/WeatherForecast');
    myTrend.add(res.timings.waiting);

    // Validate response status
    check(res, { 'status was 200': (r) => r.status == 200 });
    sleep(1);
}