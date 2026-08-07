import { create } from 'zustand';

export type GlobalModal = 'create-delivery' | null;

interface UiState {
  activeModal: GlobalModal;
  openModal: (modal: Exclude<GlobalModal, null>) => void;
  closeModal: () => void;
  reset: () => void;
}

export const useUiStore = create<UiState>((set) => ({
  activeModal: null,
  openModal: (activeModal) => set({ activeModal }),
  closeModal: () => set({ activeModal: null }),
  reset: () => set({ activeModal: null }),
}));
