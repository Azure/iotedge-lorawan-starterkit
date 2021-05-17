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