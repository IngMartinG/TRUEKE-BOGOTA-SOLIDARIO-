import { levantar } from './ambiente';

export default async function preparacion(): Promise<void> {
  await levantar();
}
