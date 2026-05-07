<script setup lang="ts">
import { ChevronDown, CreditCard, LogOut, Menu, Search, User } from 'lucide-vue-next'
import { ref } from 'vue'
import { useRouter } from 'vue-router'

defineProps<{
  onToggleSidebar: () => void
}>()

const router = useRouter()
const searchQuery = ref('')
const accountDropdownOpen = ref(false)

function handleSearch(e: Event) {
  e.preventDefault()
  console.warn('Search:', searchQuery.value)
}

function toggleAccountDropdown() {
  accountDropdownOpen.value = !accountDropdownOpen.value
}

function closeAccountDropdown() {
  accountDropdownOpen.value = false
}

function navigateToBilling() {
  router.push('/billing')
  closeAccountDropdown()
}

function handleLogout() {
  console.warn('Logout clicked')
  closeAccountDropdown()
}
</script>

<template>
  <nav class="bg-card border-b sticky top-0 z-40">
    <div class="px-4 lg:px-8">
      <div class="flex items-center justify-between h-16">
        <div class="flex items-center gap-4">
          <button
            class="lg:hidden p-2 hover:bg-accent rounded-md"
            aria-label="Toggle menu"
            @click="onToggleSidebar"
          >
            <Menu :size="20" />
          </button>

          <form class="hidden md:block" @submit="handleSearch">
            <div class="relative">
              <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <input
                v-model="searchQuery"
                type="search"
                placeholder="Search..."
                class="pl-10 pr-4 py-2 bg-background border rounded-md focus:outline-none focus:ring-2 focus:ring-primary w-64 lg:w-96"
              >
            </div>
          </form>
        </div>

        <div class="flex items-center gap-2">
          <button
            class="hidden lg:block p-2 hover:bg-accent rounded-md"
            aria-label="Toggle sidebar"
            @click="onToggleSidebar"
          >
            <Menu :size="20" />
          </button>

          <div class="relative">
            <button
              class="flex items-center gap-2 px-3 py-2 hover:bg-accent rounded-md"
              aria-label="Account menu"
              @click="toggleAccountDropdown"
            >
              <div class="w-8 h-8 rounded-full bg-primary text-primary-foreground flex items-center justify-center">
                <User :size="18" />
              </div>
              <span class="hidden sm:block text-sm font-medium">Account</span>
              <ChevronDown :size="16" class="hidden sm:block" />
            </button>

            <div
              v-if="accountDropdownOpen"
              class="fixed inset-0 z-40"
              @click="closeAccountDropdown"
            />

            <transition
              enter-active-class="transition ease-out duration-100"
              enter-from-class="transform opacity-0 scale-95"
              enter-to-class="transform opacity-100 scale-100"
              leave-active-class="transition ease-in duration-75"
              leave-from-class="transform opacity-100 scale-100"
              leave-to-class="transform opacity-0 scale-95"
            >
              <div
                v-if="accountDropdownOpen"
                class="absolute right-0 mt-2 w-56 bg-card border rounded-md shadow-lg py-1 z-50"
              >
                <div class="px-4 py-3 border-b">
                  <p class="text-sm font-medium">
                    John Doe
                  </p>
                  <p class="text-xs text-muted-foreground">
                    john@example.com
                  </p>
                </div>

                <button
                  class="w-full flex items-center gap-2 px-4 py-2 text-sm hover:bg-accent"
                  @click="navigateToBilling"
                >
                  <CreditCard :size="16" />
                  <span>Billing</span>
                </button>

                <button
                  class="w-full flex items-center gap-2 px-4 py-2 text-sm hover:bg-accent text-red-600"
                  @click="handleLogout"
                >
                  <LogOut :size="16" />
                  <span>Logout</span>
                </button>
              </div>
            </transition>
          </div>
        </div>
      </div>

      <div class="md:hidden pb-3">
        <form @submit="handleSearch">
          <div class="relative">
            <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="searchQuery"
              type="search"
              placeholder="Search..."
              class="w-full pl-10 pr-4 py-2 bg-background border rounded-md focus:outline-none focus:ring-2 focus:ring-primary"
            >
          </div>
        </form>
      </div>
    </div>
  </nav>
</template>
