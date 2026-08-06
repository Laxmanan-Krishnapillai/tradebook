import { http, HttpResponse } from 'msw';
import type { CreatePhysicalDeliveryResponse } from '../api/generated/create-physical-delivery-response';

type CreateDeliveryBody = { contractInstanceId?: string };

export const handlers = [
  http.post('/api/v1/deliveries', async ({ request }) => {
    const body = (await request.json()) as CreateDeliveryBody;
    const response: CreatePhysicalDeliveryResponse = {
      deliveryId: '11111111-2222-3333-4444-555555555555',
      contractInstanceId: body.contractInstanceId ?? 'BFEX45.BT.2301.CO2E-9-2023',
      invoiceAmountEur: 0,
      status: 'Pending - No Invoice',
      version: 1,
      createdAt: new Date().toISOString()
    };

    return HttpResponse.json(response, { status: 201 });
  }),
  http.get('/api/v1/deliveries', () => HttpResponse.json([]))
];
