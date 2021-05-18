const request = require('supertest')
const app = require('../app.routes')


describe('DecoderValueSensor', () => {
  it('should decode basic data', async () => {
    const res = await request(app)
      .get('/api/DecoderValueSensor')
      .query({
        payload: "QUJDREUxMjM0NQ==",
        fport: 1,
        devEui: "0000000000000000",
      })
      .send();
    expect(res.statusCode).toEqual(200);
    expect(res.body).toEqual({
        value: "ABCDE12345",
    });
  })
})

describe('loravisionshield', () => {
  it('should decode on', async () => {
    const res = await request(app)
      .get('/api/loravisionshield')
      .query({
        payload: "MQ==",
        fport: 1,
        devEui: "0000000000000000",
      })
      .send();
    expect(res.statusCode).toEqual(200);
    expect(res.body).toEqual({
        value: {ledState: "on"},
    });
  }),
  it('should decode off', async () => {
    const res = await request(app)
      .get('/api/loravisionshield')
      .query({
        payload: "MA==",
        fport: 1,
        devEui: "0000000000000000",
      })
      .send();
    expect(res.statusCode).toEqual(200);
    expect(res.body).toEqual({
        value: {ledState: "off"},
    });
  })
})

describe('tpl110-0292', () => {
  it('should decode parking status occupied', async () => {
    const res = await request(app)
      .get('/api/tpl110-0292')
      .query({
        payload: "MQ==",
        fport: 1,
        devEui: "0000000000000000",
      })
      .send();
    expect(res.statusCode).toEqual(200);
    expect(res.body).toEqual({
        value: {type: "parking status", occupied: true},
    });
  })
})