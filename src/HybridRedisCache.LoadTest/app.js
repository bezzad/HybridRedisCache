'use strict';

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { randomString, randomIntBetween } from './helper.js';
const BASE_URL = "https://localhost:7037/WeatherForecast";
const myTrend = new Trend('waiting_time');

console.log('HybridCache Load Test with Grafano/K6');

export const options = {
    vus: 20, // virtual users (VUs)
    executor: 'ramping-arrival-rate',
    preAllocatedVUs: 60,
    timeUnit: '1s',
    startRate: 50,
    // Ramp the number of virtual users up and down
    stages: [
        { target: 200, duration: '30s' }, // linearly go from 50 iters/s to 200 iters/s for 30s
        { target: 50, duration: '0' }, // instantly jump to 500 iters/s
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
    const requestConfigWithTag = (tag) => ({
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

    group('Add data to the Redis', () => {

        const payload = {
            id: randomIntBetween(0, 123123123),
            date: Date.UTC,
            temperatureC: 22,
            summary: `Name ${randomString(10)}`
        }
        
        const res = http.post(BASE_URL, JSON.stringify(payload), requestConfigWithTag({ name: 'POST' }));
       // console.log(res);
        //const res = http.get(BASE_URL + "/1", requestConfigWithTag({ name: 'GET' }));
        //const res = http.request("PATCH", BASE_URL, payload, requestConfigWithTag({ name: 'POST' }));

        // Validate response status
        check(res, { 'status was 200': (r) => r.status == 200 });
    })

    //group('Public GET endpoints', () => {
    //    const responses = http.batch([
    //        ['GET', `${BASE_URL}/`, null],
    //        ['GET', `${BASE_URL}/1/`, null],
    //        ['GET', `${BASE_URL}/2/`, null],
    //        ['GET', `${BASE_URL}/3/`, null],
    //        ['GET', `${BASE_URL}/4/`, null],
    //        ['GET', `${BASE_URL}/5/`, null],
    //        ['GET', `${BASE_URL}/6/`, null],
    //        ['GET', `${BASE_URL}/7/`, null],
    //        ['GET', `${BASE_URL}/8/`, null],
    //        ['GET', `${BASE_URL}/9/`, null],
    //    ]);

    //    const temperatures = Object.values(responses).map((res) => res.json('temperatureC'));

    //    // Functional test
    //    check(temperatures, {
    //        'Weathers temperatures are more than 10°C': Math.min(...temperatures) > 10,
    //    });
    //});

    //group('Create and modify crocs', () => {
    //    let URL = `${BASE_URL}/WeatherForecast`;
    //    const payload = {
    //        id: randomIntBetween(0, 2123123123),
    //        date: Date.UTC,
    //        temperatureC: 22,
    //        summary: `Name ${randomString(10)}`
    //    }

    //    group('Create weathers', () => {
    //        const res = http.put(URL, payload, requestConfigWithTag({ name: 'Create' }));

    //        if (check(res, { 'Weather created correctly': (r) => r.status === 200 })) {
    //            console.log(`Create a Weather (id: ${res.json('id')})`);
    //        } else {
    //            console.log(`Unable to create a Weather ${res.status} ${res.body}`);
    //            return;
    //        }
    //    });

    //    group('Update weather', () => {
    //        payload.date = Date.UTC;
    //        payload.date = Date.temperatureC = 33;
    //        payload.date = Date.summary = 'New name';

    //        const res = http.patch(URL, payload, requestConfigWithTag({ name: 'Update' }));
    //        const isSuccessfulUpdate = check(res, {
    //            'Update worked': () => res.status === 200,
    //            'Updated name is correct': () => res.json('summary') === 'New name',
    //            'Updated temp is correct': () => res.json('temperatureC') === 33,
    //        });

    //        if (!isSuccessfulUpdate) {
    //            console.log(`Unable to update the weather ${res.status} ${res.body}`);
    //            return;
    //        }
    //    });

    //    group('Delete weather', () => {
    //        const delRes = http.del(URL + `/${payload.id}`, null, requestConfigWithTag({ name: 'Delete' }));
    //        const isSuccessfulDelete = check(null, {
    //            'Weather was deleted correctly': () => delRes.status === 200,
    //        });

    //        if (!isSuccessfulDelete) {
    //            console.log(`Weather was not deleted properly`);
    //            return;
    //        }
    //    });
    //});

    sleep(1);
}

//export default function () {
//    var res = http.get(`${BASE_URL}/WeatherForecast`);
//    myTrend.add(res.timings.waiting);

//    // Validate response status
//    check(res, { 'status was 200': (r) => r.status == 200 });

//    res = http.post('https://localhost:7037/WeatherForecast/addWeatherForecasts/50', [
//        {
//            "id": 1,
//            "date": "2023-05-15T08:32:55.0979184+03:30",
//            "temperatureC": 22,
//            "summary": "Cold"
//        }
//    ]);
//    myTrend.add(res.timings.waiting);

//    // Validate response status
//    check(res, { 'status was 200': (r) => r.status == 200 });
//    sleep(1);
//}