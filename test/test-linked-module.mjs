const { add } = process._linkedBinding('test-linked');
export const result = add(1, 2);
