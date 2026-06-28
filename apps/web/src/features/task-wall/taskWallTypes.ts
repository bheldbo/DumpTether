import type { CreateTaskItemRequest } from '../../types';

export type CreateTaskItemOptions = Partial<CreateTaskItemRequest> & {
  workspaceId?: string | null;
};
